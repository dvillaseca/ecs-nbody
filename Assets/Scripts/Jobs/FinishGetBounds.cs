using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace nbody
{
	[BurstCompile]
	public struct FinishGetBounds : IJob
	{
		public NativeQueue<Bounds> bounds;
		public NativeArray<Bounds> finalBound;
		[ReadOnly]
		public int limit;
		public void Execute()
		{
			float3 min = new float3(limit, limit, limit);
			float3 max = new float3(-limit, -limit, -limit);
			while (bounds.Count > 0)
			{
				var b = bounds.Dequeue();
				Utils.GetLimit(ref min, ref max, b.min);
				Utils.GetLimit(ref min, ref max, b.max);
			}
			min -= 2f;
			max += 2f;
			finalBound[0] = new Bounds()
			{
				min = min,
				max = max
			};
		}
	}
}