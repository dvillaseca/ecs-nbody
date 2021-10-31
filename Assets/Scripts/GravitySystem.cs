using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

public class GravitySystem : SystemBase
{
	const float gravityConst = 0.00000667408f;
	const float particleMass = 10f;
	private const float EPSILON = 0.00001f;
	private const float COMPLEXITY = 5f;
	private static readonly float3 START_SCALE = new float3(0.05f, 0.05f, 0.05f);

	NativeQueue<Bounds> bounds;


	private static void GetLimit(ref float3 min, ref float3 max, float3 vec)
	{
		max = math.max(max, vec);
		min = math.min(min, vec);
	}
	public struct Bounds
	{
		public float3 min;
		public float3 max;
	}

	[BurstCompile]
	private struct GetBounds : IJobParallelForBatch
	{
		public NativeQueue<Bounds>.ParallelWriter bounds;
		[ReadOnly]
		public NativeArray<Body> bodies;
		[ReadOnly]
		public int limit;
		public void Execute(int start, int end)
		{
			float3 min = new float3(limit, limit, limit);
			float3 max = new float3(-limit, -limit, -limit);
			for (int i = start; i < end; i++)
			{
				GetLimit(ref min, ref max, bodies[i].position);
			}
			bounds.Enqueue(new Bounds()
			{
				min = min,
				max = max
			});
		}
	}

	[BurstCompile]
	private struct FinishGetBounds : IJob
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
				GetLimit(ref min, ref max, b.min);
				GetLimit(ref min, ref max, b.max);
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
	[BurstCompile]
	private struct GenerateTreeJob : IJob
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
				AddBody(0, bodies[i].position, particleMass);
			}
		}
		private void AverageBodys(ref LinearOctNode node, float3 pos, float mass)
		{
			float m = mass + node.avgMass;
			node.avgPos = (node.avgPos * node.avgMass + pos * mass) * (1f / m);
			node.avgMass = m;
		}
		private void AddBody(int nodeIndex, float3 pos, float mass)
		{
			var node = nodes[nodeIndex];
			int index;
			if (node.type == LinearOctNode.NodeType.Internal)
			{
				AverageBodys(ref node, pos, mass);
				index = GetIndex(ref node, pos);
				AddBody(index, pos, mass);
				nodes[nodeIndex] = node;
				return;
			}
			if (node.type == LinearOctNode.NodeType.None)
			{
				AverageBodys(ref node, pos, mass);
				node.type = LinearOctNode.NodeType.External;
				nodes[nodeIndex] = node;
				return;
			}
			Split(ref node);

			index = GetIndex(ref node, node.avgPos);
			AddBody(index, node.avgPos, node.avgMass);

			AverageBodys(ref node, pos, mass);
			index = GetIndex(ref node, pos);
			AddBody(index, pos, mass);

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
	[BurstCompile]
	private struct AttractAndMove : IJobChunk
	{
		public ComponentTypeHandle<LocalToWorld> transformHandle;
		public ComponentTypeHandle<Body> bodyHandle;
		[ReadOnly]
		[DeallocateOnJobCompletion]
		public NativeArray<LinearOctNode> nodes;
		[ReadOnly]
		public float deltaTime;
		[ReadOnly]
		public float deltaForce;

		public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int index)
		{
			var transforms = batchInChunk.GetNativeArray(transformHandle);
			var bodies = batchInChunk.GetNativeArray(bodyHandle);
			var length = transforms.Length;
			for (int i = 0; i < length; i++)
			{
				var body = bodies[i];

				Interact(0, ref body);
				body.position += body.velocity * deltaTime;
				bodies[i] = body;

				var transform = transforms[i];
				transform.Value = float4x4.TRS(body.position, quaternion.identity, START_SCALE);
				transforms[i] = transform;
			}
		}
		private void Interact(int index, ref Body body)
		{
			var node = nodes[index];
			if (node.type == LinearOctNode.NodeType.None)
				return;
			var force = node.avgPos - body.position;
			if (math.abs(force.x) < EPSILON && math.abs(force.y) < EPSILON && math.abs(force.z) < EPSILON)
				return;
			float mag = math.lengthsq(force);
			if (node.type == LinearOctNode.NodeType.Internal && node.sSize / mag > COMPLEXITY)
			{
				for (int i = node.childsStartIndex; i < node.childsStartIndex + 8; i++)
				{
					Interact(i, ref body);
				}
				return;
			}
			float dist = mag;
			if (dist < 0.002f)
				dist = 0.002f;
			//if node is internal the distance from the avg mass could be really tiny and add an insane speed
			float strength = deltaForce * node.avgMass / (dist * math.sqrt(mag));
			body.velocity += force * strength;
		}
	}
	protected override void OnStartRunning()
	{
		bounds = new NativeQueue<Bounds>(Allocator.Persistent);
	}
	protected override void OnStopRunning()
	{
		if (bounds.IsCreated)
			bounds.Dispose();
	}
	protected override void OnUpdate()
	{
		var query = GetEntityQuery(typeof(Body));
		var bodies = query.ToComponentDataArrayAsync<Body>(Allocator.TempJob, out JobHandle dep);
		var deps = JobHandle.CombineDependencies(dep, Dependency);

		bounds.Clear();
		var boundJob = new GetBounds()
		{
			limit = 1000,
			bodies = bodies,
			bounds = bounds.AsParallelWriter()
		};
		var boundJobHandler = boundJob.ScheduleBatch(bodies.Length, bodies.Length / 16, deps);
		var finalBound = new NativeArray<Bounds>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var finalBoundJob = new FinishGetBounds()
		{
			bounds = bounds,
			finalBound = finalBound,
			limit = 1000,
		};
		var finalBoundHandle = finalBoundJob.Schedule(boundJobHandler);

		var nodes = new NativeArray<LinearOctNode>(400000, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var generateTreeJob = new GenerateTreeJob()
		{
			bodies = bodies,
			nodes = nodes,
			bounds = finalBound,
			current = 0,
		};
		var generateTreeHandle = generateTreeJob.Schedule(finalBoundHandle);

		var deltaTime = Time.DeltaTime;
		var deltaForce = gravityConst * deltaTime;
		var attractAndMoveJob = new AttractAndMove()
		{
			bodyHandle = GetComponentTypeHandle<Body>(false),
			transformHandle = GetComponentTypeHandle<LocalToWorld>(false),
			deltaTime = deltaTime,
			deltaForce = deltaForce,
			nodes = nodes
		};
		Dependency = attractAndMoveJob.ScheduleParallel(query, generateTreeHandle);
	}
}
