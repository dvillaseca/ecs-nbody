using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameOfLife : MonoBehaviour
{
	public enum SourceType
	{
		random,
		pattern,
		texture,
	}
	public Vector2Int resolution = new Vector2Int(400, 400);
	public ComputeShader shader;
	public SourceType sourceType;
	public int frameRate = 0;
	public float perlinSeedMagnitude = .1f;

	public Texture2D sourceTexture;
	public GOLPattern sourcePattern;


	private RenderTexture rt1;
	private RenderTexture rt2;
	private bool twoIsTarget;
	// Start is called before the first frame update
	void Awake()
	{
		Application.targetFrameRate = frameRate;

		switch (sourceType)
		{
			case SourceType.random:
				Texture2D src = new Texture2D(resolution.x, resolution.y);
				var r1 = Random.value * perlinSeedMagnitude;
				var r2 = Random.value * perlinSeedMagnitude;
				for (int i = 0; i < resolution.x; i++)
					for (int j = 0; j < resolution.y; j++)
						src.SetPixel(i, j, new Color(Mathf.PerlinNoise(i * r1, j * r2), 1, 1));
				src.Apply();
				sourceTexture = src;
				break;
			case SourceType.pattern:
				sourceTexture = sourcePattern.GetTexture();
				break;
		}

		resolution = new Vector2Int(sourceTexture.width, sourceTexture.height);

		rt1 = new RenderTexture(resolution.x, resolution.y, 24);
		rt1.antiAliasing = 1;
		rt1.enableRandomWrite = true;

		rt2 = new RenderTexture(resolution.x, resolution.y, 24);
		rt2.antiAliasing = 1;
		rt2.enableRandomWrite = true;

		Graphics.Blit(sourceTexture, rt2);
		//	twoIsTarget = true;

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
