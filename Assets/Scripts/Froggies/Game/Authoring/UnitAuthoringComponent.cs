﻿using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;

namespace Froggies
{
	public enum UnitType
	{
		None = 0,
		Harvester = 1 << 0,
		Melee = 1 << 1,
		Ranged = 1 << 2
	}

	[RequireComponent(typeof(PhysicsBodyAuthoring))]
	[RequireComponent(typeof(PhysicsShapeAuthoring))]
	public class UnitAuthoringComponent : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
	{
		[HideInInspector] public Transform unitCentreTransform;
		[HideInInspector] public UnitType unitType;
		[HideInInspector] public UnitMove unitMove;
		[HideInInspector] public FreezeRotation freezeRotation;
		[HideInInspector] public Health health;
		[HideInInspector] public Resistances resistances;
		[HideInInspector] public Harvester harvester;
		[HideInInspector] public CombatUnit combatUnit;
		[HideInInspector] public RangedUnit rangedUnit;
		[HideInInspector] public ProjectileAuthoringComponent projectileGameObject;
		[HideInInspector] public Transform projectileSpawnTransform;
		[HideInInspector] public bool isEnemy;

		public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
		{
			if (projectileGameObject)
				referencedPrefabs.Add(projectileGameObject.gameObject);
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			unitMove.turnRate *= Mathf.Deg2Rad;
			dstManager.AddComponentData(entity, unitMove);
			dstManager.AddComponentData(entity, freezeRotation);
			dstManager.AddComponentData(entity, new UnitTag());
			dstManager.AddComponentData(entity, new PathFinding());
			dstManager.AddComponentData(entity, new Attackable { centreOffset = unitCentreTransform.position - transform.position });
			dstManager.AddComponentData(entity, new Flocker());
			dstManager.AddBuffer<PathNode>(entity);

			dstManager.AddComponentData(entity, new CurrentTarget { targetData = new TargetData() });
			dstManager.AddComponentData(entity, new PreviousTarget { targetData = new TargetData() });
			dstManager.AddComponentData(entity, new CurrentAIState { currentAIState = AIState.Idle });

			dstManager.AddComponentData(entity, health);
			dstManager.AddComponentData(entity, resistances);

			if ((unitType & UnitType.Harvester) != 0)
				dstManager.AddComponentData(entity, harvester);

			if ((unitType & (UnitType.Melee | UnitType.Ranged)) != 0)
				dstManager.AddComponentData(entity, combatUnit);

			if ((unitType & UnitType.Melee) != 0)
				dstManager.AddComponentData(entity, new MeleeUnit());

			if ((unitType & UnitType.Ranged) != 0)
			{
				rangedUnit.projectile = conversionSystem.GetPrimaryEntity(projectileGameObject);
				rangedUnit.projectileSpawnOffset = projectileSpawnTransform.position - transform.position;
				dstManager.AddComponentData(entity, rangedUnit);
			}

			if (isEnemy)
			{
				dstManager.AddComponentData(entity, new EnemyTag());
				dstManager.AddComponentData(entity, new TargetableByAI { targetType = AITargetType.Enemy });
			}
			else
			{
				dstManager.AddBuffer<Command>(entity);
			}
		}
	}
}