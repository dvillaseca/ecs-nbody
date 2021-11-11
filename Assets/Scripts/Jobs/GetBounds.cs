using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace nbody
{
	public struct Bounds
	{
		public float3 min;
		public float3 max;
	}
	[BurstCompile]
	public struct GetBounds : IJobParallelForBatch
	{
		public NativeQueue<Bounds>.ParallelWriter bounds;
		[ReadOnly]
		[Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction]
		public NativeArray<Body> bodies;
		[ReadOnly]
		public int limit;
		public void Execute(int start, int count)
		{
			float3 min = new float3(limit, limit, limit);
			float3 max = new float3(-limit, -limit, -limit);
			int end = math.min(start + count, bodies.Length);
			for (int i = start; i < end; i++)
			{
				Utils.GetLimit(ref min, ref max, bodies[i].position);
			}
			bounds.Enqueue(new Bounds()
			{
				min = min,
				max = max
			});
		}
	}
}