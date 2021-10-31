using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public struct Body : IComponentData
{
	public float mass;
	public float3 velocity;
}
