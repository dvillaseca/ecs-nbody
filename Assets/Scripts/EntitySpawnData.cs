using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[GenerateAuthoringComponent]
public struct EntitySpawnData : IComponentData
{
	public Entity prefab;
	public int count;
	public float radius;
}
