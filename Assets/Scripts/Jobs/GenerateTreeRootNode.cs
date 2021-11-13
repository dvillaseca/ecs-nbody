using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;

namespace nbody
{
	[BurstCompile]
	public struct GenerateTreeRootNode : IJob
	{
		[NativeDisableContainerSafetyRestriction]
		public NativeArray<LinearOctNode> nodes;
		[ReadOnly]
		[DeallocateOnJobCompletion]
		public NativeArray<Bounds> finalBound;
		public void Execute()
		{
			float3 min = finalBound[0].min;
			float3 max = finalBound[0].max;
			float newSize = math.max(math.max((max.x - min.x), (max.y - min.y)), max.z - min.z);
			float3 center = (min + max) * 0.5f;

			var rootNode = new LinearOctNode(center, newSize);
			rootNode.type = LinearOctNode.NodeType.Internal;
			rootNode.childsStartIndex = 1;
			rootNode.avgPos = float3.zero;
			for (int i = 1; i < 9; i++)
			{
				float m = nodes[i].avgMass + rootNode.avgMass;
				rootNode.avgPos = (rootNode.avgPos * rootNode.avgMass + nodes[i].avgPos * nodes[i].avgMass) * (1f / m);
				rootNode.avgMass = m;
			}
			nodes[0] = rootNode;
		}
	}
}