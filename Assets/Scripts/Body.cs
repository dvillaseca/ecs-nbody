
using Unity.Entities;
using Unity.Mathematics;

namespace nbody
{
	public struct Body : IComponentData
	{
		public float mass;
		public float3 velocity;
		public float3 position;
	}
}