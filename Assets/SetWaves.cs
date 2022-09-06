using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

[ExecuteAlways]
public class SetWaves : MonoBehaviour
{
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
	[SerializeField]
	public Wave[] _waves;
	[SerializeField]
	private ComputeBuffer waveBuffer;

	private static readonly int WaveDataBuffer = Shader.PropertyToID("_WaveDataBuffer");

	public Renderer renderer;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    private void OnEnable()
    {
	    renderer.material.EnableKeyword("USE_STRUCTURED_BUFFER");
	    waveBuffer?.Dispose();
	    waveBuffer = new ComputeBuffer(10, (sizeof(float) * 6));
	    waveBuffer.SetData(_waves);
	    renderer.material.SetBuffer(WaveDataBuffer, waveBuffer);
	    //GetComponent<Renderer>().materials[0] = mat;
    }

    private void OnDisable()
    {
	    //throw new NotImplementedException();
    }

    // Update is called once per frame
    void Update()
    {
		
	}
}
