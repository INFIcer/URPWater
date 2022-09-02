using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static DepthOnly;
[ExecuteAlways]
public class DepthOnlyCam : MonoBehaviour
{
	public Camera cam;
	private void OnEnable()
	{
		CustomRenderPass.SetCam(cam);
	}
	private void OnDisable()
	{
		CustomRenderPass.SetCam(null);
	}
	// Start is called before the first frame update
	void Start()
	{

	}

	// Update is called once per frame
	void Update()
	{

	}
}
