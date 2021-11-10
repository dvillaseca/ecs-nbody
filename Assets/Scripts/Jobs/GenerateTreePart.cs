using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace nbody
{
	[Unity.Burst.BurstCompile]
	public struct GenerateTreePart : IJob
	{
		[NativeDisableContainerSafetyRestriction]
		public NativeArray<LinearOctNode> nodes;
		[ReadOnly]
		public NativeArray<Body> bodies;
		[ReadOnly]
		public NativeArray<Bounds> bounds;

		public int rootIndex;
		public int current;

		public void Execute()
		{
			float3 min = bounds[0].min;
			float3 max = bounds[0].max;
			float newSize = math.max(math.max((max.x - min.x), (max.y - min.y)), max.z - min.z) * 0.5f;
			float3 center = (min + max) * 0.5f;
			switch (rootIndex)
			{
				case 1:
					nodes[rootIndex] = (new LinearOctNode(new float3(center.x - newSize * 0.5f, center.y - newSize * 0.5f, center.z - newSize * 0.5f), newSize));
					break;
				case 2:
					nodes[rootIndex] = (new LinearOctNode(new float3(center.x - newSize * 0.5f, center.y - newSize * 0.5f, center.z + newSize * 0.5f), newSize));
					break;
				case 3:
					nodes[rootIndex] = (new LinearOctNode(new float3(center.x + newSize * 0.5f, center.y - newSize * 0.5f, center.z - newSize * 0.5f), newSize));
					break;
				case 4:
					nodes[rootIndex] = (new LinearOctNode(new float3(center.x + newSize * 0.5f, center.y - newSize * 0.5f, center.z + newSize * 0.5f), newSize));
					break;
				case 5:
					nodes[rootIndex] = (new LinearOctNode(new float3(center.x - newSize * 0.5f, center.y + newSize * 0.5f, center.z - newSize * 0.5f), newSize));
					break;
				case 6:
					nodes[rootIndex] = (new LinearOctNode(new float3(center.x - newSize * 0.5f, center.y + newSize * 0.5f, center.z + newSize * 0.5f), newSize));
					break;
				case 7:
					nodes[rootIndex] = (new LinearOctNode(new float3(center.x + newSize * 0.5f, center.y + newSize * 0.5f, center.z - newSize * 0.5f), newSize));
					break;
				case 8:
					nodes[rootIndex] = (new LinearOctNode(new float3(center.x + newSize * 0.5f, center.y + newSize * 0.5f, center.z + newSize * 0.5f), newSize));
					break;
			}

			current = (rootIndex - 1) * Const.TREE_SIZE + 10;
			for (int i = 0; i < bodies.Length; i++)
			{
				AddBody(rootIndex, bodies[i].position, bodies[i].mass, bodies[i].velocity);
			}
		}
		private void AverageBodys(ref LinearOctNode node, float3 pos, float mass)
		{
			float m = mass + node.avgMass;
			node.avgPos = (node.avgPos * node.avgMass + pos * mass) * (1f / m);
			node.avgMass = m;
		}
		private void AddBody(int nodeIndex, float3 pos, float mass, float3 velocity)
		{
			var node = nodes[nodeIndex];
			int index;
			if (node.type == LinearOctNode.NodeType.Internal)
			{
				AverageBodys(ref node, pos, mass);
				index = GetIndex(ref node, pos);
				AddBody(index, pos, mass, velocity);
				nodes[nodeIndex] = node;
				return;
			}
			if (node.type == LinearOctNode.NodeType.None)
			{
				AverageBodys(ref node, pos, mass);
				node.type = LinearOctNode.NodeType.External;
				node.bodySize = Utils.MassToSize(mass);
				//node.bodyVelocity = velocity;
				nodes[nodeIndex] = node;
				return;
			}
			Split(ref node);

			index = GetIndex(ref node, node.avgPos);
			AddBody(index, node.avgPos, node.avgMass, velocity);

			AverageBodys(ref node, pos, mass);
			index = GetIndex(ref node, pos);
			AddBody(index, pos, mass, velocity);

			nodes[nodeIndex] = node;
		}
		private int GetIndex(ref LinearOctNode node, float3 pos)
		{
			int index = 0;
			if (pos.y > node.center.y)
				index |= 4;
			if (pos.x > node.center.x)
				index |= 2;
			if (pos.z > node.center.z)
				index |= 1;
			return index + node.childsStartIndex;
		}
		private void Split(ref LinearOctNode node)
		{
			float newSize = node.size * 0.5f;
			node.childsStartIndex = current;
			nodes[current++] = (new LinearOctNode(new float3(node.center.x - newSize * 0.5f, node.center.y - newSize * 0.5f, node.center.z - newSize * 0.5f), newSize));
			nodes[current++] = (new LinearOctNode(new float3(node.center.x - newSize * 0.5f, node.center.y - newSize * 0.5f, node.center.z + newSize * 0.5f), newSize));
			nodes[current++] = (new LinearOctNode(new float3(node.center.x + newSize * 0.5f, node.center.y - newSize * 0.5f, node.center.z - newSize * 0.5f), newSize));
			nodes[current++] = (new LinearOctNode(new float3(node.center.x + newSize * 0.5f, node.center.y - newSize * 0.5f, node.center.z + newSize * 0.5f), newSize));
			nodes[current++] = (new LinearOctNode(new float3(node.center.x - newSize * 0.5f, node.center.y + newSize * 0.5f, node.center.z - newSize * 0.5f), newSize));
			nodes[current++] = (new LinearOctNode(new float3(node.center.x - newSize * 0.5f, node.center.y + newSize * 0.5f, node.center.z + newSize * 0.5f), newSize));
			nodes[current++] = (new LinearOctNode(new float3(node.center.x + newSize * 0.5f, node.center.y + newSize * 0.5f, node.center.z - newSize * 0.5f), newSize));
			nodes[current++] = (new LinearOctNode(new float3(node.center.x + newSize * 0.5f, node.center.y + newSize * 0.5f, node.center.z + newSize * 0.5f), newSize));
			node.type = LinearOctNode.NodeType.Internal;
		}
	}
}