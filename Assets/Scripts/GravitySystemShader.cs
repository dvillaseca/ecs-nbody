using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;

namespace nbody
{
	[DisableAutoCreation]
	public class GravitySystemShader : SystemBase
	{
		private NativeArray<Body> bodies;
		private NativeArray<LinearOctNode> nodes;
		private ComputeBuffer bodiesBuffer;
		private CancellationTokenSource cancelToken;
		private NativeQueue<Bounds> bounds;
		private bool updated = false;

		protected override void OnStartRunning()
		{
			var spawnData = GetSingleton<EntitySpawnData>();
			float explosionDiameter = math.pow(spawnData.count * math.pow(Utils.MassToSize(spawnData.massRange.y), 3f), 0.3333333f);
			bodies = new NativeArray<Body>(spawnData.count, Allocator.Persistent);
			for (int i = 0; i < spawnData.count; i++)
			{
				var newEntity = EntityManager.Instantiate(spawnData.prefab);
				EntityManager.AddComponent<BodyTag>(newEntity);

				float mass = UnityEngine.Random.Range(spawnData.massRange.y, spawnData.massRange.x);
				var size = Utils.MassToSize(mass);
				var pos = UnityEngine.Random.insideUnitSphere;
				Body body = default;
				switch (spawnData.option)
				{
					case EntitySpawnData.EmitOption.explosion:
						pos *= explosionDiameter;
						body = new Body { velocity = pos * spawnData.explosionForce, mass = mass, position = pos, size = size };
						break;
					case EntitySpawnData.EmitOption.disk:

						pos.y *= spawnData.diskRadius * 0.5f;
						pos.x *= spawnData.diskRadius;
						pos.z *= spawnData.diskRadius;

						Vector3 v = Vector3.Cross(pos.normalized, Vector3.up);
						v = v.normalized * math.sqrt(Const.GRAVITY * spawnData.massRange.y / pos.magnitude) * spawnData.diskSpeed;
						body = new Body { velocity = v, mass = mass, position = pos, size = size };
						break;
				}
				bodies[i] = body;
				//EntityManager.SetComponentData(newEntity, body);
			}
			bodiesBuffer = new ComputeBuffer(bodies.Length, sizeof(float) * 8);
			bodiesBuffer.SetData(bodies);
			cancelToken = new CancellationTokenSource();
			bounds = new NativeQueue<Bounds>(Allocator.Persistent);
			nodes = new NativeArray<LinearOctNode>(Const.TREE_SIZE * Const.TREE_COUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
			_ = Update(cancelToken.Token);
		}
		protected override void OnStopRunning()
		{
			cancelToken.Cancel();
			DisposeAll();
		}
		private void DisposeAll()
		{
			if (bodies.IsCreated)
				bodies.Dispose();
			if (bounds.IsCreated)
				bounds.Dispose();
			if (nodes.IsCreated)
				nodes.Dispose();
			bodiesBuffer.Release();
		}

		int sampleCount = 0;
		long treeMs = 0l;
		long shaderMs = 0l;
		long copyMs = 0l;
		System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
		private async Task Update(CancellationToken token)
		{
			while (!token.IsCancellationRequested)
			{
				watch.Start();
				bounds.Clear();
				var boundJob = new GetBounds()
				{
					limit = 1000,
					bodies = bodies,
					bounds = bounds.AsParallelWriter()
				};
				var boundJobHandler = boundJob.ScheduleBatch(bodies.Length, bodies.Length / 16);
				var finalBound = new NativeArray<Bounds>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
				var finalBoundJob = new FinishGetBounds()
				{
					bounds = bounds,
					finalBound = finalBound,
					limit = 1000,
				};
				var finalBoundHandle = finalBoundJob.Schedule(boundJobHandler);

				var bLists = new NativeList<Body>[8];
				for (int i = 0; i < 8; i++)
				{
					bLists[i] = new NativeList<Body>(bodies.Length / 2, Allocator.Persistent);
				}
				var sortJob = new SortBodies()
				{
					bodies = bodies,
					bounds = finalBound,
					b0 = bLists[0].AsParallelWriter(),
					b1 = bLists[1].AsParallelWriter(),
					b2 = bLists[2].AsParallelWriter(),
					b3 = bLists[3].AsParallelWriter(),
					b4 = bLists[4].AsParallelWriter(),
					b5 = bLists[5].AsParallelWriter(),
					b6 = bLists[6].AsParallelWriter(),
					b7 = bLists[7].AsParallelWriter()
				};
				var sortJobHandler = sortJob.ScheduleBatch(bodies.Length, bodies.Length / 16, finalBoundHandle);
				var wipeNodesJob = new WipeNodes()
				{
					nodes = nodes
				};
				var wipeNodesHandle = wipeNodesJob.ScheduleBatch(nodes.Length, nodes.Length / 16, sortJobHandler);
				wipeNodesHandle.Complete();

				var treeJobs = new NativeArray<JobHandle>(8, Allocator.Persistent);

				for (int i = 0; i < 8; i++)
				{
					var generateTreeJob = new GenerateTreePart()
					{
						bodies = bLists[i].AsArray(),
						nodes = nodes,
						bounds = finalBound,
						rootIndex = i + 1
					};
					treeJobs[i] = generateTreeJob.Schedule();
				}
				JobHandle.CombineDependencies(treeJobs).Complete();
				treeJobs.Dispose();
				for (int i = 0; i < 8; i++)
					bLists[i].Dispose();

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
				finalBound.Dispose();

				watch.Stop();
				treeMs += watch.ElapsedMilliseconds;
				watch.Reset();

				var deltaTime = Time.DeltaTime;
				await RunComputeShader(.1f);
				//bodies.Dispose();
				//bodies = new NativeArray<Body>(result, Allocator.Persistent);
				//result.Dispose();
				updated = true;

				sampleCount++;
				if (sampleCount == 10)
				{
					Debug.Log("tree time: " + treeMs * .1);
					Debug.Log("copy time: " + copyMs * .1);
					Debug.Log("shader time: " + shaderMs * .1);
					treeMs = 0;
					shaderMs = 0;
					copyMs = 0;
					sampleCount = 0;
				}
				await Task.Yield();
			}
			DisposeAll();
		}
		private async Task RunComputeShader(float dt)
		{
			watch.Start();
			var computeShader = ComputeShaderTest.Instance.computeShaderTree;

			var kernelIndex = computeShader.FindKernel("CSApplyForces");
			var nodeSize = sizeof(float) * 10 + sizeof(int) * 2;
			ComputeBuffer nodesBuffer = new ComputeBuffer(nodes.Length, nodeSize);
			nodesBuffer.SetData(nodes);

			computeShader.SetBuffer(kernelIndex, "nodes", nodesBuffer);
			computeShader.SetBuffer(kernelIndex, "bodies", bodiesBuffer);
			computeShader.SetFloat("deltaTime", dt);

			watch.Stop();
			copyMs += watch.ElapsedMilliseconds;
			watch.Reset();

			watch.Start();

			computeShader.Dispatch(kernelIndex, bodies.Length / 64, 1, 1);

			//lots of weird stuff going on here to avoid a unity bug https://forum.unity.com/threads/asyncgpureadback-requestintonativearray-causes-invalidoperationexception-on-nativearray.1011955/
			var taskCompletionSource = new TaskCompletionSource<AsyncGPUReadbackRequest>();
			//var tempArray = new NativeArray<Body>(bodies.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
			AsyncGPUReadback.RequestIntoNativeArray(ref bodies, bodiesBuffer, (req) => taskCompletionSource.SetResult(req));
			await taskCompletionSource.Task;
			nodesBuffer.Release();

			watch.Stop();
			shaderMs += watch.ElapsedMilliseconds;
			watch.Reset();
		}
		protected override void OnUpdate()
		{
			if (!updated)
				return;
			var query = GetEntityQuery(typeof(BodyTag));
			var attractAndMoveJob = new DisplayBodies()
			{
				transformHandle = GetComponentTypeHandle<LocalToWorld>(false),
				bd = bodies
			};
			Dependency = attractAndMoveJob.ScheduleParallel(query, Dependency);
			updated = false;
		}
	}
}