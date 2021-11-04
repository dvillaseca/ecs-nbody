using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

public class ComputeShaderTest : MonoBehaviour
{
	public ComputeShader computeShader;
	public ComputeShader computeShaderTree;
	public GameObject prefab;
	public int count = 1000;
	public float startSpeed = 2f;
	public float diameter = 10f;
	public Transform[] bodiesTrans;
	public nbody.Body[] bodiesData;
	public static ComputeShaderTest Instance;
	private void Awake()
	{
		Instance = this;
	}
	// Start is called before the first frame update
	//void Start()
	//{
	//	bodiesTrans = new Transform[count];
	//	bodiesData = new nbody.Body[count];
	//	for (int i = 0; i < count; i++)
	//	{
	//		var pos = Random.insideUnitSphere;
	//		bodiesData[i] = new nbody.Body { velocity = pos * startSpeed, mass = 1, position = pos * diameter, size = 1 };
	//		bodiesTrans[i] = Instantiate(prefab).transform;
	//	}
	//}
	//void UpdatePos()
	//{
	//	var size = sizeof(float) * 8;
	//	ComputeBuffer bodiesBuffer = new ComputeBuffer(bodiesData.Length, size);
	//	bodiesBuffer.SetData(bodiesData);
	//	computeShader.SetBuffer(0, "bodies", bodiesBuffer);
	//	computeShader.SetFloat("deltaTime", Time.deltaTime);
	//	computeShader.SetFloat("resolution", bodiesData.Length);
	//	computeShader.Dispatch(0, bodiesData.Length / 10, 1, 1);

	//	bodiesBuffer.GetData(bodiesData);
	//	bodiesBuffer.Dispose();
	//}
	//private void OnDestroy()
	//{
	//	//bodiesData.Dispose();
	//}

	//// Update is called once per frame
	//void Update()
	//{
	//	UpdatePos();
	//	for (int i = 0; i < bodiesData.Length; i++)
	//	{
	//		bodiesTrans[i].position = bodiesData[i].position;
	//	}
	//}
}
