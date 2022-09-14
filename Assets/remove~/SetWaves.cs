using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

[ExecuteAlways]
public class SetWaves : MonoBehaviour
{
	[SerializeField] public WaterSurfaceData surfaceData;

	[System.Serializable]
	public struct Wave
	{
		public float amplitude; // height of the wave in units(m)
		public float direction; // direction the wave travels in degrees from Z+
		public float wavelength; // distance between crest>crest
		public float2 origin; // Omi directional point of origin
		public float onmiDir; // Is omni?

		public Wave(float amp, float dir, float length, float2 org, bool omni)
		{
			amplitude = amp;
			direction = dir;
			wavelength = length;
			origin = org;
			onmiDir = omni ? 1 : 0;
		}
	}

	private bool _useComputeBuffer;
	public bool computeOverride;

	[SerializeField] RenderTexture _depthTex;
	public Texture bakedDepthTex;
	private Camera _depthCam;
	private Texture2D _rampTexture;
	[SerializeField]
	public Wave[] _waves;
	[SerializeField]
	private ComputeBuffer waveBuffer;
	private float _maxWaveHeight;
	private float _waveHeight;

	[SerializeField]
	public WaterSettingsData settingsData;
	[SerializeField]
	public WaterSurfaceData surfaceData;
	[SerializeField]
	private WaterResources resources;

	private static readonly int CameraRoll = Shader.PropertyToID("_CameraRoll");
	private static readonly int InvViewProjection = Shader.PropertyToID("_InvViewProjection");
	private static readonly int WaterDepthMap = Shader.PropertyToID("_WaterDepthMap");
	private static readonly int FoamMap = Shader.PropertyToID("_FoamMap");
	private static readonly int SurfaceMap = Shader.PropertyToID("_SurfaceMap");
	private static readonly int WaveHeight = Shader.PropertyToID("_WaveHeight");
	private static readonly int MaxWaveHeight = Shader.PropertyToID("_MaxWaveHeight");
	private static readonly int MaxDepth = Shader.PropertyToID("_MaxDepth");
	private static readonly int WaveCount = Shader.PropertyToID("_WaveCount");
	private static readonly int CubemapTexture = Shader.PropertyToID("_CubemapTexture");
	private static readonly int WaveDataBuffer = Shader.PropertyToID("_WaveDataBuffer");
	private static readonly int WaveData = Shader.PropertyToID("waveData");
	private static readonly int AbsorptionScatteringRamp = Shader.PropertyToID("_AbsorptionScatteringRamp");
	private static readonly int DepthCamZParams = Shader.PropertyToID("_VeraslWater_DepthCamParams");

	private Renderer renderer;

	public float _waterMaxVisibility;

	public int randomSeed;

	// Start is called before the first frame update
	void Start()
	{
		renderer = GetComponent<Renderer>();
	}


	private bool _useComputeBuffer;

	private void OnEnable()
	{
		if (!computeOverride)
			_useComputeBuffer = SystemInfo.supportsComputeShaders &&
			                    Application.platform != RuntimePlatform.WebGLPlayer &&
			                    Application.platform != RuntimePlatform.Android;
		else
			_useComputeBuffer = false;
		Init();
		Shader.EnableKeyword("USE_STRUCTURED_BUFFER");
		waveBuffer?.Dispose();
		waveBuffer = new ComputeBuffer(10, (sizeof(float) * 6));
		waveBuffer.SetData(_waves);
		Shader.SetGlobalInt(WaveCount, _waves.Length);
		renderer.material.SetBuffer(WaveDataBuffer, waveBuffer);
		//GetComponent<Renderer>().materials[0] = mat;
	}

	public void Init()
	{
		SetWaves(false);
		GenerateColorRamp();
		if (bakedDepthTex)
		{
			Shader.SetGlobalTexture(WaterDepthMap, bakedDepthTex);
		}

		// if (!gameObject.TryGetComponent(out _planarReflections))
		// {
		// 	_planarReflections = gameObject.AddComponent<PlanarReflections>();
		// }
		// _planarReflections.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector;
		// _planarReflections.m_settings = settingsData.planarSettings;
		// _planarReflections.enabled = settingsData.refType == ReflectionType.PlanarReflection;

		if (resources == null)
		{
			resources = Resources.Load("WaterResources") as WaterResources;
		}

		if (Application.platform != RuntimePlatform.WebGLPlayer) // TODO - bug with Opengl depth
			CaptureDepthMap();
	}

	private Texture2D _rampTexture;

	private void GenerateColorRamp()
	{
		if (_rampTexture == null)
			_rampTexture = new Texture2D(128, 4, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.None);
		_rampTexture.wrapMode = TextureWrapMode.Clamp;

		var defaultFoamRamp = resources.defaultFoamRamp;

		var cols = new Color[512];
		for (var i = 0; i < 128; i++)
		{
			cols[i] = surfaceData._absorptionRamp.Evaluate(i / 128f);
		}

		for (var i = 0; i < 128; i++)
		{
			cols[i + 128] = surfaceData._scatterRamp.Evaluate(i / 128f);
		}

		for (var i = 0; i < 128; i++)
		{
			// switch(surfaceData._foamSettings.foamType)
			// {
			// 	case 0: // default
			cols[i + 256] = defaultFoamRamp.GetPixelBilinear(i / 128f, 0.5f);
			// 		break;
			// 	case 1: // simple
			// 		cols[i + 256] = defaultFoamRamp.GetPixelBilinear(surfaceData._foamSettings.basicFoam.Evaluate(i / 128f) , 0.5f);
			// 		break;
			// 	case 2: // custom
			// 		cols[i + 256] = Color.black;
			// 		break;
			// }
		}

		_rampTexture.SetPixels(cols);
		_rampTexture.Apply();
		Shader.SetGlobalTexture(AbsorptionScatteringRamp, _rampTexture);
	}

	private void SetWaves()
	{
		SetupWaves(surfaceData._customWaves);

		// set default resources
		Shader.SetGlobalTexture(FoamMap, resources.defaultFoamMap);
		Shader.SetGlobalTexture(SurfaceMap, resources.defaultSurfaceMap);

		_maxWaveHeight = 0f;
		foreach (var w in _waves)
		{
			_maxWaveHeight += w.amplitude;
		}

		_maxWaveHeight /= _waves.Length;

		_waveHeight = transform.position.y;

		Shader.SetGlobalFloat(WaveHeight, _waveHeight);
		Shader.SetGlobalFloat(MaxWaveHeight, _maxWaveHeight);
		Shader.SetGlobalFloat(MaxDepth, surfaceData._waterMaxVisibility);

		switch (settingsData.refType)
		{
			case ReflectionType.Cubemap:
				Shader.EnableKeyword("_REFLECTION_CUBEMAP");
				Shader.DisableKeyword("_REFLECTION_PROBES");
				Shader.DisableKeyword("_REFLECTION_PLANARREFLECTION");
				Shader.SetGlobalTexture(CubemapTexture, settingsData.cubemapRefType);
				break;
			case ReflectionType.ReflectionProbe:
				Shader.DisableKeyword("_REFLECTION_CUBEMAP");
				Shader.EnableKeyword("_REFLECTION_PROBES");
				Shader.DisableKeyword("_REFLECTION_PLANARREFLECTION");
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}

		Shader.SetGlobalInt(WaveCount, _waves.Length);

		//GPU side
		if (_useComputeBuffer)
		{
			Shader.EnableKeyword("USE_STRUCTURED_BUFFER");
			waveBuffer?.Dispose();
			waveBuffer = new ComputeBuffer(10, (sizeof(float) * 6));
			waveBuffer.SetData(_waves);
			Shader.SetGlobalBuffer(WaveDataBuffer, waveBuffer);
		}
		else
		{
			Shader.DisableKeyword("USE_STRUCTURED_BUFFER");
			Shader.SetGlobalVectorArray(WaveData, GetWaveData());
		}

		//CPU side
		// if (GerstnerWavesJobs.Initialized == false && Application.isPlaying)
		// 	GerstnerWavesJobs.Init();
	}

	private void SetupWaves(bool custom)
	{
		//if (!custom)
		{
			//create basic waves based off basic wave settings
			var backupSeed = Random.state;
			Random.InitState(randomSeed);
			var basicWaves = surfaceData._basicWaveSettings;
			var a = basicWaves.amplitude;
			var d = basicWaves.direction;
			var l = basicWaves.wavelength;
			var numWave = basicWaves.numWaves;
			_waves = new Wave[numWave];

			var r = 1f / numWave;

			for (var i = 0; i < numWave; i++)
			{
				var p = Mathf.Lerp(0.5f, 1.5f, i * r);
				var amp = a * p * Random.Range(0.8f, 1.2f);
				var dir = d + Random.Range(-90f, 90f);
				var len = l * p * Random.Range(0.6f, 1.4f);
				_waves[i] = new Wave(amp, dir, len, Vector2.zero, false);
				Random.InitState(randomSeed + i + 1);
			}

			Random.state = backupSeed;
		}
	}

	private void OnDisable()
	{
		//throw new NotImplementedException();
	}

	[SerializeField] RenderTexture _depthTex;
	private Camera _depthCam;

	[ContextMenu("Capture Depth")]
	public void CaptureDepthMap()
	{
		//Generate the camera
		if (_depthCam == null)
		{
			var go =
				new GameObject("depthCamera") { hideFlags = HideFlags.HideAndDontSave }; //create the cameraObject
			_depthCam = go.AddComponent<Camera>();
		}

		var additionalCamData = _depthCam.GetUniversalAdditionalCameraData();
		additionalCamData.renderShadows = false;
		additionalCamData.requiresColorOption = CameraOverrideOption.Off;
		additionalCamData.requiresDepthOption = CameraOverrideOption.Off;

		var t = _depthCam.transform;
		var depthExtra = 4.0f;
		t.position = Vector3.up * (transform.position.y + depthExtra); //center the camera on this water plane height
		t.up = Vector3.forward; //face the camera down

		_depthCam.enabled = true;
		_depthCam.orthographic = true;
		_depthCam.orthographicSize = 250; //hardcoded = 1k area - TODO
		_depthCam.nearClipPlane = 0.01f;
		_depthCam.farClipPlane = _waterMaxVisibility + depthExtra;
		_depthCam.allowHDR = false;
		_depthCam.allowMSAA = false;
		_depthCam.cullingMask = (1 << 10);
		//Generate RT
		if (!_depthTex)
			_depthTex = new RenderTexture(1024, 1024, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
		if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 ||
		    SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
		{
			_depthTex.filterMode = FilterMode.Point;
		}

		_depthTex.wrapMode = TextureWrapMode.Clamp;
		_depthTex.name = "WaterDepthMap";
		//do depth capture
		_depthCam.targetTexture = _depthTex;
		_depthCam.Render();
		Shader.SetGlobalTexture(WaterDepthMap, _depthTex);
		// set depth bufferParams for depth cam(since it doesnt exist and only temporary)
		var _params = new Vector4(t.position.y, 250, 0, 0);
		//Vector4 zParams = new Vector4(1-f/n, f/n, (1-f/n)/f, (f/n)/f);//2015
		Shader.SetGlobalVector(DepthCamZParams, _params);

		_depthCam.enabled = false;
		_depthCam.targetTexture = null;
	}
}