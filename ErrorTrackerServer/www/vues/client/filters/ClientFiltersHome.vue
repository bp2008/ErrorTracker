<template>
	<div class="clientFiltersHome">
		<FilterList v-if="!filterId" :projectName="projectName" />
		<FilterDetails v-else :projectName="projectName" :filterId="parseInt(filterId)" />
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