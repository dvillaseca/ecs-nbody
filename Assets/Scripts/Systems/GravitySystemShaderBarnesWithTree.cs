//using System.Threading;
//using System.Threading.Tasks;
//using Unity.Collections;
//using Unity.Entities;
//using Unity.Jobs;
//using Unity.Mathematics;
//using Unity.Transforms;
//using UnityEngine;
//using UnityEngine.Rendering;

//namespace nbody
//{
//	public class GravitySystemShaderBarnesWithTree : SystemBase
//	{
//		private NativeArray<Body> bodies;
//		private ComputeBuffer bodiesBuffer;
//		private ComputeBuffer nodesBuffer;
//		private ComputeBuffer indexesBuffer;
//		private CancellationTokenSource cancelToken;
//		protected override void OnStartRunning()
//		{
//			var spawnData = GetSingleton<EntitySpawnData>();
//			float explosionDiameter = math.pow(spawnData.count * math.pow(Utils.MassToSize(spawnData.massRange.y), 3f), 0.3333333f);
//			bodies = new NativeArray<Body>(spawnData.count, Allocator.Persistent);
//			for (int i = 0; i < spawnData.count; i++)
//			{
//				var newEntity = EntityManager.Instantiate(spawnData.prefab);
//				EntityManager.AddComponent<BodyTag>(newEntity);

//				float mass = UnityEngine.Random.Range(spawnData.massRange.y, spawnData.massRange.x);
//				var size = Utils.MassToSize(mass);
//				var pos = UnityEngine.Random.insideUnitSphere;
//				Body body = default;
//				switch (spawnData.option)
//				{
//					case EntitySpawnData.EmitOption.explosion:
//						pos *= explosionDiameter;
//						body = new Body { velocity = pos * spawnData.explosionForce, mass = mass, position = pos, size = size };
//						break;
//					case EntitySpawnData.EmitOption.disk:

//						pos.y *= spawnData.diskRadius * 0.5f;
//						pos.x *= spawnData.diskRadius;
//						pos.z *= spawnData.diskRadius;

//						Vector3 v = Vector3.Cross(pos.normalized, Vector3.up);
//						v = v.normalized * math.sqrt(Const.GRAVITY * spawnData.massRange.y / pos.magnitude) * spawnData.diskSpeed;
//						body = new Body { velocity = v, mass = mass, position = pos, size = size };
//						break;
//				}
//				bodies[i] = body;
//				//EntityManager.SetComponentData(newEntity, body);
//			}
//			bodiesBuffer = new ComputeBuffer(bodies.Length, sizeof(float) * 8);
//			bodiesBuffer.SetData(bodies);

//			var nodeSize = sizeof(float) * 10 + sizeof(int) * 2;
//			var nodes = new NativeArray<LinearOctNode>(treeSize * treeCount, Allocator.Persistent);
//			nodesBuffer = new ComputeBuffer(nodes.Length, nodeSize);
//			nodesBuffer.SetData(nodes);

//			indexesBuffer = new ComputeBuffer(treeCount, sizeof(int));
//			indexesBuffer.SetData(new int[treeCount]);

//			cancelToken = new CancellationTokenSource();
//			bounds = new NativeQueue<Bounds>(Allocator.Persistent);
//			_ = Update(cancelToken.Token);
//		}
//		protected override void OnStopRunning()
//		{
//			cancelToken.Cancel();
//			if (bodies.IsCreated)
//				bodies.Dispose();
//			if (bounds.IsCreated)
//				bounds.Dispose();
//			bodiesBuffer.Release();
//			nodesBuffer.Release();
//		}

//		[Unity.Burst.BurstCompile]
//		public struct AttractAndMove : IJobChunk
//		{
//			public ComponentTypeHandle<LocalToWorld> transformHandle;
//			[ReadOnly]
//			public NativeArray<Body> bd;
//			[ReadOnly]
//			public float deltaTime;

//			public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int index)
//			{
//				var transforms = batchInChunk.GetNativeArray(transformHandle);
//				var length = transforms.Length;
//				for (int i = 0; i < length; i++)
//				{
//					var body = bd[index + i];
//					var transform = transforms[i];
//					transform.Value = float4x4.TRS(body.position, quaternion.identity, 0.05f);
//					transforms[i] = transform;
//				}
//			}
//		}
//		[Unity.Burst.BurstCompile]
//		public struct GenerateTree : IJobParallelForBatch
//		{
//			[ReadOnly]
//			public NativeArray<Body> bodies;
//			[DeallocateOnJobCompletion]
//			[ReadOnly]
//			public NativeArray<Bounds> bounds;

//			public NativeArray<LinearOctNode> nodes;

//			[ReadOnly]
//			public int batchesCount;
//			[ReadOnly]
//			public int treeSize;

//			public void Execute(int startIndex, int count)
//			{
//				float3 min = bounds[0].min;
//				float3 max = bounds[0].max;
//				float sized = math.max(math.max((max.x - min.x), (max.y - min.y)), max.z - min.z);
//				float3 center = (min + max) * 0.5f;
//				int rootNodeIndex = startIndex;
//				nodes[rootNodeIndex] = new LinearOctNode(center, sized);
//				int current = rootNodeIndex + 1;
//				int bodyCount = (int)math.ceil(bodies.Length / batchesCount);

//				int batchIndex = (int)math.floor(startIndex / treeSize);
//				int bodyStartIndex = bodyCount * batchIndex;
//				int bodyEndIndex = math.min(bodyCount * (batchIndex + 1), bodies.Length);
//				for (int i = bodyStartIndex; i < bodyEndIndex; i++)
//				{
//					AddBody(rootNodeIndex, bodies[i].position, bodies[i].mass, bodies[i].velocity, ref current);
//				}
//			}
//			private void AverageBodys(ref LinearOctNode node, float3 pos, float mass)
//			{
//				float m = mass + node.avgMass;
//				node.avgPos = (node.avgPos * node.avgMass + pos * mass) * (1f / m);
//				node.avgMass = m;
//			}
//			private void AddBody(int nodeIndex, float3 pos, float mass, float3 velocity, ref int currentIndex)
//			{
//				var node = nodes[nodeIndex];
//				int index;
//				if (node.type == LinearOctNode.NodeType.Internal)
//				{
//					AverageBodys(ref node, pos, mass);
//					index = GetIndex(ref node, pos);
//					AddBody(index, pos, mass, velocity, ref currentIndex);
//					nodes[nodeIndex] = node;
//					return;
//				}
//				if (node.type == LinearOctNode.NodeType.None)
//				{
//					AverageBodys(ref node, pos, mass);
//					node.type = LinearOctNode.NodeType.External;
//					node.bodySize = Utils.MassToSize(mass);
//					//node.bodyVelocity = velocity;
//					nodes[nodeIndex] = node;
//					return;
//				}
//				Split(ref node, ref currentIndex);

//				index = GetIndex(ref node, node.avgPos);
//				AddBody(index, node.avgPos, node.avgMass, velocity, ref currentIndex);

//				AverageBodys(ref node, pos, mass);
//				index = GetIndex(ref node, pos);
//				AddBody(index, pos, mass, velocity, ref currentIndex);

//				nodes[nodeIndex] = node;
//			}
//			private int GetIndex(ref LinearOctNode node, float3 pos)
//			{
//				int index = 0;
//				if (pos.y > node.center.y)
//					index |= 4;
//				if (pos.x > node.center.x)
//					index |= 2;
//				if (pos.z > node.center.z)
//					index |= 1;
//				return index + node.childsStartIndex;
//			}
//			private void Split(ref LinearOctNode node, ref int current)
//			{
//				float newSize = node.size * 0.5f;
//				node.childsStartIndex = current;
//				nodes[current++] = (new LinearOctNode(new float3(node.center.x - newSize * 0.5f, node.center.y - newSize * 0.5f, node.center.z - newSize * 0.5f), newSize));
//				nodes[current++] = (new LinearOctNode(new float3(node.center.x - newSize * 0.5f, node.center.y - newSize * 0.5f, node.center.z + newSize * 0.5f), newSize));
//				nodes[current++] = (new LinearOctNode(new float3(node.center.x + newSize * 0.5f, node.center.y - newSize * 0.5f, node.center.z - newSize * 0.5f), newSize));
//				nodes[current++] = (new LinearOctNode(new float3(node.center.x + newSize * 0.5f, node.center.y - newSize * 0.5f, node.center.z + newSize * 0.5f), newSize));
//				nodes[current++] = (new LinearOctNode(new float3(node.center.x - newSize * 0.5f, node.center.y + newSize * 0.5f, node.center.z - newSize * 0.5f), newSize));
//				nodes[current++] = (new LinearOctNode(new float3(node.center.x - newSize * 0.5f, node.center.y + newSize * 0.5f, node.center.z + newSize * 0.5f), newSize));
//				nodes[current++] = (new LinearOctNode(new float3(node.center.x + newSize * 0.5f, node.center.y + newSize * 0.5f, node.center.z - newSize * 0.5f), newSize));
//				nodes[current++] = (new LinearOctNode(new float3(node.center.x + newSize * 0.5f, node.center.y + newSize * 0.5f, node.center.z + newSize * 0.5f), newSize));
//				node.type = LinearOctNode.NodeType.Internal;
//			}
//		}
//		private NativeQueue<Bounds> bounds;
//		const int treeCount = 4;
//		const int treeSize = 300000;
//		private bool updated = false;
//		private async Task Update(CancellationToken token)
//		{
//			while (!token.IsCancellationRequested)
//			{
//				bounds.Clear();
//				var boundJob = new GetBounds()
//				{
//					limit = 1000,
//					bodies = bodies,
//					bounds = bounds.AsParallelWriter()
//				};
//				var boundJobHandler = boundJob.ScheduleBatch(bodies.Length, bodies.Length / 16);
//				var finalBound = new NativeArray<Bounds>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
//				var finalBoundJob = new FinishGetBounds()
//				{
//					bounds = bounds,
//					finalBound = finalBound,
//					limit = 1000,
//				};
//				var finalBoundHandle = finalBoundJob.Schedule(boundJobHandler);
//				finalBoundHandle.Complete();
//				//var nodes = new NativeArray<LinearOctNode>(treeCount * treeSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
//				//var generateTreeJob = new GenerateTree()
//				//{
//				//	bodies = bodies,
//				//	nodes = nodes,
//				//	bounds = finalBound,
//				//	batchesCount = treeCount,
//				//	treeSize = treeSize
//				//};
//				//var generateTreeHandle = generateTreeJob.ScheduleBatch(nodes.Length, treeSize, finalBoundHandle);

//				//generateTreeHandle.Complete();
//				//Debug.Log("completed: " + generateTreeHandle.IsCompleted);
//				var deltaTime = Time.DeltaTime;
//				var result = await RunComputeShader(/*nodes,*/ deltaTime, finalBound[0]);
//				bodies.Dispose();
//				bodies = new NativeArray<Body>(result, Allocator.Persistent);
//				result.Dispose();
//				finalBound.Dispose();
//				//nodes.Dispose();
//				updated = true;
//				await Task.Yield();
//			}
//		}
//		private async Task<NativeArray<Body>> RunComputeShader(/*NativeArray<LinearOctNode> nodes, */float dt, Bounds bounds)
//		{
//			var computeShader = ComputeShaderTest.Instance.computeShaderTree;

//			var kernelIndex = computeShader.FindKernel("CSWipeTree");
//			computeShader.SetBuffer(kernelIndex, "nodes", nodesBuffer);
//			computeShader.Dispatch(kernelIndex, nodesBuffer.count / 64, 1, 1);

//			kernelIndex = computeShader.FindKernel("CSGenerateTree");
//			computeShader.SetBuffer(kernelIndex, "nodes", nodesBuffer);
//			computeShader.SetBuffer(kernelIndex, "bodies", bodiesBuffer);
//			computeShader.SetBuffer(kernelIndex, "currentNodeIndex", indexesBuffer);
//			computeShader.SetInt("treeCount", treeCount);
//			computeShader.SetInt("treeSize", treeSize);
//			computeShader.SetInt("bodyCount", bodies.Length);
//			computeShader.SetVector("boundsMin", new float4(bounds.min, 0));
//			computeShader.SetVector("boundsMax", new float4(bounds.max, 0));
//			computeShader.Dispatch(kernelIndex, 1, 1, 1);

//			kernelIndex = computeShader.FindKernel("CSApplyForces");
//			//var nodeSize = sizeof(float) * 10 + sizeof(int) * 2;
//			//ComputeBuffer nodesBuffer = new ComputeBuffer(nodes.Length, nodeSize);
//			//nodesBuffer.SetData(nodes);

//			computeShader.SetBuffer(kernelIndex, "nodes", nodesBuffer);
//			computeShader.SetBuffer(kernelIndex, "bodies", bodiesBuffer);
//			computeShader.SetFloat("deltaTime", dt);
//			computeShader.SetInt("treeCount", treeCount);
//			computeShader.SetInt("treeSize", treeSize);
//			computeShader.Dispatch(kernelIndex, bodies.Length / 64, 1, 1);


//			//lots of weird stuff going on here to avoid a unity bug https://forum.unity.com/threads/asyncgpureadback-requestintonativearray-causes-invalidoperationexception-on-nativearray.1011955/
//			var taskCompletionSource = new TaskCompletionSource<AsyncGPUReadbackRequest>();
//			var tempArray = new NativeArray<Body>(bodies.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
//			AsyncGPUReadback.RequestIntoNativeArray(ref tempArray, bodiesBuffer, (req) => taskCompletionSource.SetResult(req));
//			await taskCompletionSource.Task;
//			//nodesBuffer.Release();
//			return tempArray;
//		}
//		//private async Task Update(CancellationToken token)
//		//{
//		//	while (!token.IsCancellationRequested)
//		//	{
//		//		var deltaTime = Time.DeltaTime;
//		//		var result = await RunComputeShader(deltaTime);
//		//		bodies.Dispose();
//		//		bodies = new NativeArray<Body>(result, Allocator.Persistent);
//		//		result.Dispose();
//		//		updated = true;
//		//		await Task.Yield();
//		//	}
//		//}
//		//private async Task<NativeArray<Body>> RunComputeShader(float dt)
//		//{
//		//	var computeShader = ComputeShaderTest.Instance.computeShader;
//		//	computeShader.SetBuffer(0, "bodies", bodiesBuffer);
//		//	computeShader.SetFloat("deltaTime", dt);
//		//	computeShader.SetInt("bodyCount", bodies.Length);
//		//	computeShader.Dispatch(0, bodies.Length / 256, 1, 1);

//		//	//lots of weird stuff going on here to avoid a unity bug https://forum.unity.com/threads/asyncgpureadback-requestintonativearray-causes-invalidoperationexception-on-nativearray.1011955/
//		//	var taskCompletionSource = new TaskCompletionSource<AsyncGPUReadbackRequest>();
//		//	var tempArray = new NativeArray<Body>(bodies.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
//		//	AsyncGPUReadback.RequestIntoNativeArray(ref tempArray, bodiesBuffer, (req) => taskCompletionSource.SetResult(req));
//		//	await taskCompletionSource.Task;
//		//	return tempArray;
//		//}
//		protected override void OnUpdate()
//		{
//			var query = GetEntityQuery(typeof(BodyTag));
//			//if (bodies != null && bodies.IsCreated)
//			//	bodies.Dispose();
//			//bodies = query.ToComponentDataArray<Body>(Allocator.Persistent);
//			//var blength = bodies.Length;

//			//var bounds = new Bounds { min = new float3(1000, 1000, 1000), max = new float3(-1000, -1000, -1000) };
//			//for (int i = 0; i < blength; i++)
//			//{
//			//	Utils.GetLimit(ref bounds.min, ref bounds.max, bodies[i].position);
//			//}
//			//var nodes = new NativeArray<LinearOctNode>(2000000, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
//			//var generateTree = new GenerateTree()
//			//{
//			//	bodies = bodies,
//			//	bounds = bounds,
//			//	nodes = nodes,
//			//	current = 0,
//			//};
//			//generateTree.Execute();

//			//bodiesData.Dispose();
//			//nodes.Dispose();
//			if (!updated)
//				return;
//			var attractAndMoveJob = new AttractAndMove()
//			{
//				transformHandle = GetComponentTypeHandle<LocalToWorld>(false),
//				bd = bodies
//			};
//			Dependency = attractAndMoveJob.ScheduleParallel(query, Dependency);
//			updated = false;
//		}
//	}
//}