using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[CreateAssetMenu]
public class GOLPattern : ScriptableObject
{
	[TextArea]
	public string pattern;
	public Vector2Int resolution;
	public Vector2Int textureResolution;
	public Texture2D GetTexture()
	{
		float[] transformed = new float[resolution.x * resolution.y];
		string num = "";
		int column = 0;
		int row = 0;
		int parsedNum;
		for (int i = 0; i < pattern.Length; i++)
		{
			var c = pattern[i];
			if (char.IsDigit(c))
			{
				num += pattern[i];
			}
			else if (c == 'o' || c == 'b')
			{
				if (!string.IsNullOrEmpty(num))
					parsedNum = int.Parse(num);
				else
					parsedNum = 1;
				var targetIndex = column + parsedNum;
				while (column < targetIndex)
				{
					transformed[row * resolution.x + column] = c == 'o' ? 1f : 0f;
					column++;
				}
				num = "";
			}
			else if (c == '$')
			{
				if (!string.IsNullOrEmpty(num))
					parsedNum = int.Parse(num);
				else
					parsedNum = 1;
				row += parsedNum;
				num = "";
				column = 0;
			}
		}

		var potResolution = resolution;

		var potBase = 1;
		while (Mathf.Pow(8, potBase) < potResolution.x)
		{
			potBase++;
		}
		potResolution.x = (int)Mathf.Pow(8, potBase);

		potBase = 1;
		while (Mathf.Pow(8, potBase) < potResolution.y)
		{
			potBase++;
		}
		potResolution.y = (int)Mathf.Pow(8, potBase);
		if (textureResolution.x != 0 || textureResolution.y != 0)
			potResolution = textureResolution;
		Debug.Log(potResolution);
		Texture2D texture = new Texture2D(potResolution.x, potResolution.y, TextureFormat.RGBA32, false);
		texture.filterMode = FilterMode.Point;

		for (int i = 0; i < potResolution.x; i++)
			for (int j = 0; j < potResolution.y; j++)
				texture.SetPixel(i, j, Color.black);

		var offset = (potResolution - resolution) / 2;
		for (int y = 0; y < resolution.y; y++)
		{
			for (int x = 0; x < resolution.x; x++)
			{
				var alive = transformed[x + y * resolution.x];
				texture.SetPixel(x + offset.x, resolution.y - 1 - y + offset.y, new Color(alive, alive, alive, 1));
			}
		}
		texture.Apply();
		return texture;
	}
}
