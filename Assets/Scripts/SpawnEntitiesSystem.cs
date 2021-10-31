using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;

public class SpawnEntitiesSystem : SystemBase
{
	protected override void OnStartRunning()
	{
		var spawnData = GetSingleton<EntitySpawnData>();
		for (int i = 0; i < spawnData.count; i++)
		{
			var newEntity = EntityManager.Instantiate(spawnData.prefab);
			var random = UnityEngine.Random.insideUnitSphere * spawnData.radius;
			//var pos = new Translation { Value = random };
			//EntityManager.AddComponent<Translation>(newEntity);
			//EntityManager.SetComponentData(newEntity, pos);
			//	EntityManager.AddComponent<PhysicsVelocity>(newEntity);
			//EntityManager.SetComponentData(newEntity, new PhysicsVelocity { Linear = UnityEngine.Random.insideUnitSphere });
			EntityManager.AddComponent<Body>(newEntity);
			EntityManager.SetComponentData(newEntity, new Body { velocity = UnityEngine.Random.insideUnitSphere, mass = 1, position = random });
		}
	}
	protected override void OnUpdate()
	{

	}
}
