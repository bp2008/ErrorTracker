<template>
	<div class="advancedSearchHome">
		<div class="inputSection conditions">
			<label class="inputLabel">Advanced Search Conditions</label>
			<div class="conditionList">
				<FilterCondition v-for="condition in filter.conditions"
								 :key="condition.FilterConditionId"
								 :condition="condition"
								 @delete="deleteCondition"
								 class="condition" />
			</div>
			<div>
				<input type="button" value="New Condition (and Commit Changes)" @click="newCondition" />
			</div>
		</div>
	</div>
</template>

<script>
	import FilterList from 'appRoot/vues/client/filters/FilterList.vue';
	import FilterDetails from 'appRoot/vues/client/filters/FilterDetails.vue';

	export default {
		components: { FilterList, FilterDetails },
		props:
		{
			projectName: {
				type: String,
				default: ""
			},
			filterId: {
				type: String,
				default: ""
			}
		}
	}
</script>

<style scoped>
	.inputSection
	{
		margin-bottom: 30px;
	}

		.inputSection .inputLabel
		{
			font-size: 24px;
			font-weight: bold;
			display: block;
			color: #555555;
			margin-bottom: 4px;
		}

	.conditionList,
	.actionList
	{
		display: inline-block;
		border: 1px solid #777777;
		border-radius: 8px;
		overflow: hidden;
		min-height: 16px;
		min-width: 300px;
	}

	.conditionList
	{
		background-color: #dddde8;
	}

	.condition:nth-child(2n)
	{
		background-color: #b5b5d8;
	}
</style>