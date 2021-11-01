using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace nbody
{
	[BurstCompile]
	public struct GenerateTree : IJob
	{
		[DeallocateOnJobCompletion]
		[ReadOnly]
		public NativeArray<Body> bodies;
		[ReadOnly]
		[DeallocateOnJobCompletion]
		public NativeArray<Bounds> bounds;

		public NativeArray<LinearOctNode> nodes;
		public int current;

		public void Execute()
		{
			float3 min = bounds[0].min;
			float3 max = bounds[0].max;
			float sized = math.max(math.max((max.x - min.x), (max.y - min.y)), max.z - min.z);
			float3 center = (min + max) * 0.5f;
			nodes[0] = new LinearOctNode(center, sized);
			current++;
			int length = bodies.Length;
			for (int i = 0; i < length; i++)
			{
				AddBody(0, bodies[i].position, bodies[i].mass, bodies[i].velocity);
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
				node.bodyVelocity = velocity;
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