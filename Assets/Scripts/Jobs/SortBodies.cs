using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;

namespace nbody
{
	[Unity.Burst.BurstCompile]
	public struct SortBodies : IJobParallelForBatch
	{
		public NativeList<Body>.ParallelWriter b0;
		public NativeList<Body>.ParallelWriter b1;
		public NativeList<Body>.ParallelWriter b2;
		public NativeList<Body>.ParallelWriter b3;
		public NativeList<Body>.ParallelWriter b4;
		public NativeList<Body>.ParallelWriter b5;
		public NativeList<Body>.ParallelWriter b6;
		public NativeList<Body>.ParallelWriter b7;
		[ReadOnly]
		public NativeArray<Body> bodies;
		[ReadOnly]
		public NativeArray<Bounds> bounds;

		public void Execute(int start, int count)
		{
			float3 min = bounds[0].min;
			float3 max = bounds[0].max;
			float3 center = (min + max) * 0.5f;
			int end = math.min(start + count, bodies.Length);
			for (int i = start; i < end; i++)
			{
				var pos = bodies[i].position;
				int index = 0;
				if (pos.y > center.y)
					index |= 4;
				if (pos.x > center.x)
					index |= 2;
				if (pos.z > center.z)
					index |= 1;
				switch (index)
				{
					case 0:
						b0.AddNoResize(bodies[i]);
						break;
					case 1:
						b1.AddNoResize(bodies[i]);
						break;
					case 2:
						b2.AddNoResize(bodies[i]);
						break;
					case 3:
						b3.AddNoResize(bodies[i]);
						break;
					case 4:
						b4.AddNoResize(bodies[i]);
						break;
					case 5:
						b5.AddNoResize(bodies[i]);
						break;
					case 6:
						b6.AddNoResize(bodies[i]);
						break;
					case 7:
						b7.AddNoResize(bodies[i]);
						break;
					default:
						break;
				}
			}
		}
	}
}