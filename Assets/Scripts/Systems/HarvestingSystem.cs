﻿using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Unity.Mathematics;
using Unity.Jobs;

public class HarvestingSystem : KodeboldJobSystem
{
	private EndSimulationEntityCommandBufferSystem m_endSimECBSystem;

	public override void GetSystemDependencies(Dependencies dependencies)
	{
	}

	public override void InitSystem()
	{
		m_endSimECBSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
	}

	public override void UpdateSystem()
	{
		EntityCommandBuffer.ParallelWriter ecb = m_endSimECBSystem.CreateCommandBuffer().AsParallelWriter();

		ComponentDataFromEntity<ResourceNode> resourceNodeLookup = GetComponentDataFromEntity<ResourceNode>();

		JobHandle movingToHarvestHandle = Entities
		.WithReadOnly(resourceNodeLookup)
		.WithAll<MovingToHarvestState>()
		.ForEach((Entity entity, int entityInQueryIndex, ref Harvester harvester, ref CurrentTarget currentTarget, ref DynamicBuffer<Command> commandBuffer, in Translation translation) =>
		{
			//if (currentTarget.findTargetOfType != AITargetType.None)
			//	return;

			if (!resourceNodeLookup.TryGetComponentDataFromEntity(currentTarget.targetData.targetEntity, out ResourceNode resourceNode))
			{
				Debug.Log($"Harvest node {currentTarget.targetData.targetEntity} destroyed when moving to it, finding nearby resource node of type {currentTarget.targetData.targetType} instead");

				CommandProcessSystem.QueueCommandWithoutTarget<HarvestCommandWithoutTarget>(CommandType.HarvestWithoutTarget, currentTarget.targetData.targetType, commandBuffer);
				return;
			}

			//Get harvestable radius
			float dist = math.distance(translation.Value, currentTarget.targetData.targetPos);
			float range = resourceNode.harvestableRadius + harvester.harvestRange;

			//Are we close enough to harvest yet?
			if (dist <= range)
			{
				//Move the command onto the execution phase
				Command currentCommand = commandBuffer[0];
				currentCommand.commandStatus = CommandStatus.ExecutionPhase;
				commandBuffer[0] = currentCommand;
				Debug.Log("Begin execution of harvest command");

				//Set type we are harvesting + empty inventory if type is different
				ResourceNode resource = GetComponent<ResourceNode>(currentTarget.targetData.targetEntity);
				if (harvester.currentlyCarryingType != resource.resourceType)
				{
					Debug.Log($"Harvesting type { resource.resourceType } setting carry amount to 0");

					harvester.currentlyCarryingAmount = 0;
					harvester.currentlyCarryingType = resource.resourceType;
				}
			}
		}).ScheduleParallel(Dependency);

		float dt = Time.DeltaTime;
		EntityCommandBuffer.ParallelWriter ecb2 = m_endSimECBSystem.CreateCommandBuffer().AsParallelWriter();

		Dependency = Entities
		.WithReadOnly(resourceNodeLookup)
		.WithAll<HarvestingState>()
		.ForEach((Entity entity, int entityInQueryIndex, ref Harvester harvester, ref CurrentTarget currentTarget, ref DynamicBuffer<Command> commandBuffer) =>
		{
			if (!resourceNodeLookup.TryGetComponentDataFromEntity(currentTarget.targetData.targetEntity, out ResourceNode resourceNode))
			{
				Debug.Log($"Harvest node {currentTarget.targetData.targetEntity} destroyed while harvesting it, finding nearby resource node of type {currentTarget.targetData.targetType} instead");

				CommandProcessSystem.QueueCommandWithoutTarget<HarvestCommandWithoutTarget>(CommandType.HarvestWithoutTarget, currentTarget.targetData.targetType, commandBuffer);
				return;
			}

			//If harvest is on cd
			if (harvester.harvestTickTimer > 0)
			{
				//Cooling down
				harvester.harvestTickTimer -= dt;
				return;
			}
			//Put harvest on cd
			harvester.harvestTickTimer = harvester.harvestTickCooldown;

			//Harvest the smallest amount between amount of resource, amount harvestable and inventory space
			int inventorySpace = harvester.carryCapacity - harvester.currentlyCarryingAmount;
			int harvestAmount = math.min(math.min(resourceNode.resourceAmount, harvester.harvestAmount), inventorySpace);

			//Transfer resource from resource node to harvester
			Debug.Log($"Harvested { harvestAmount } of {resourceNode.resourceType}");
			harvester.currentlyCarryingAmount += harvestAmount;
			resourceNode.resourceAmount -= harvestAmount;

			//If the resource is empty destroy it, we must do this before deciding whether to continue harvesting or go deposit
			if (resourceNode.resourceAmount <= 0)
			{
				Debug.Log("Fully harvested resource");
				ecb2.DestroyEntity(entityInQueryIndex, currentTarget.targetData.targetEntity);
			}
			else //If the resource isn't being destroyed then update its values
			{
				ecb2.SetComponent(entityInQueryIndex, currentTarget.targetData.targetEntity, resourceNode);
			}

			//If we are at capacity go back to deposit
			if (harvester.currentlyCarryingAmount >= harvester.carryCapacity)
			{
				CommandProcessSystem.QueueCommandWithoutTarget<DepositCommandWithoutTarget>(CommandType.DepositWithoutTarget, AITargetType.Store, commandBuffer);
				return;
			}

			//If the resource is empty find a new one
			if (resourceNode.resourceAmount <= 0)
			{
				CommandProcessSystem.QueueCommandWithoutTarget<HarvestCommandWithoutTarget>(CommandType.HarvestWithoutTarget, currentTarget.targetData.targetType, commandBuffer);
				return;
			}

		}).ScheduleParallel(movingToHarvestHandle);

		m_endSimECBSystem.AddJobHandleForProducer(Dependency);
	}

	public override void FreeSystem()
	{
	}
}