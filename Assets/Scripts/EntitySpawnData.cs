using UnityEngine;
using Unity.Entities;

namespace nbody
{
	[GenerateAuthoringComponent]
	public struct EntitySpawnData : IComponentData
	{
		public enum EmitOption
		{
			disk,
			explosion
		}
		public EmitOption option;
		public Entity prefab;
		public int count;
		public float explosionForce;
		public float diskRadius;
		public float diskSpeed;
		public Vector2 massRange;
	}
}
