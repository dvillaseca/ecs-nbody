using UnityEngine;
using Unity.Entities;

namespace nbody
{
	[GenerateAuthoringComponent]
	public struct EntitySpawnData : IComponentData
	{
		public enum EmitOption
		{
			inOrbit,
			noVel,
			explosion
		}
		public EmitOption option;
		public Entity prefab;
		public int count;
		public float radius;
		public Vector2 massRange;
	}
}
