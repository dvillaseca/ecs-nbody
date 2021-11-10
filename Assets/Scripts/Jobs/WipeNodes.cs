using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;

namespace nbody
{
	[Unity.Burst.BurstCompile]
	public struct WipeNodes : IJobParallelForBatch
	{
		public NativeArray<LinearOctNode> nodes;

		public void Execute(int start, int count)
		{
			int end = math.min(start + count, nodes.Length);
			for (int i = start; i < end; i++)
			{
				nodes[i] = new LinearOctNode();
			}
		}
	}
}