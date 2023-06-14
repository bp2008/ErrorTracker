<template>
	<div class="clientFiltersHome">
		<FilterList v-if="!filterId" :projectName="projectName" :searchQuery="searchQuery" :regexSearch="regexSearch" />
		<FilterDetails v-else :projectName="projectName" :filterId="parseInt(filterId)" :searchQuery="searchQuery" :regexSearch="regexSearch" />
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
			},
			searchQuery: {
				type: String,
				default: ""
			},
			regexSearch: {
				type: Boolean,
				default: false
			}
		}
	}
</script>