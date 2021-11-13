using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameOfLife : MonoBehaviour
{
	public Vector2Int resolution = new Vector2Int(400, 400);
	public ComputeShader shader;
	public int frameRate = 0;

	private RenderTexture rt1;
	private RenderTexture rt2;
	private bool twoIsTarget;
	// Start is called before the first frame update
	void Awake()
	{
		Application.targetFrameRate = frameRate;

		rt1 = new RenderTexture(resolution.x, resolution.y, 24);
		rt1.antiAliasing = 1;
		rt1.enableRandomWrite = true;

		rt2 = new RenderTexture(resolution.x, resolution.y, 24);
		rt2.antiAliasing = 1;
		rt2.enableRandomWrite = true;

		Texture2D src = new Texture2D(resolution.x, resolution.y);
		var r1 = Random.value * 1000;
		var r2 = Random.value * 1000;
		for (int i = 0; i < resolution.x; i++)
			for (int j = 0; j < resolution.y; j++)
				src.SetPixel(i, j, new Color(Mathf.PerlinNoise(i * r1, j * r2), 1, 1));
		src.Apply();
		//RenderTexture.active = rt2;
		Graphics.Blit(src, rt2);
		//RenderTexture.active = null;
		//shader.SetTexture(0, "Result", rt);
		//	GetComponent<Camera>().targetTexture = rt;
	}
	private void OnDestroy()
	{
		rt1.Release();
		rt2.Release();
	}
	private void OnRenderImage(RenderTexture src, RenderTexture dest)
	{
		Graphics.Blit(twoIsTarget ? rt2 : rt1, null as RenderTexture);
	}

	// Update is called once per frame
	void Update()
	{
		shader.SetTexture(0, "Prev", twoIsTarget ? rt1 : rt2);
		shader.SetTexture(0, "Result", twoIsTarget ? rt2 : rt1);
		shader.SetVector("resolution", new Vector4(resolution.x, resolution.y, 0, 0));
		shader.Dispatch(0, resolution.x / 8, resolution.y / 8, 1);
		twoIsTarget = !twoIsTarget;
	}
}
