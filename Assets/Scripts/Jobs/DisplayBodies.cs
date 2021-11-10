using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;

namespace nbody
{
	[Unity.Burst.BurstCompile]
	public struct DisplayBodies : IJobChunk
	{
		public ComponentTypeHandle<LocalToWorld> transformHandle;
		[ReadOnly]
		public NativeArray<Body> bd;
		[ReadOnly]
		public float deltaTime;

		public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int index)
		{
			var transforms = batchInChunk.GetNativeArray(transformHandle);
			var length = transforms.Length;
			for (int i = 0; i < length; i++)
			{
				var body = bd[index + i];
				var transform = transforms[i];
				transform.Value = float4x4.TRS(body.position, quaternion.identity, 0.05f);
				transforms[i] = transform;
			}
		}
	}
}