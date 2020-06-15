<template>
	<div v-if="!projectName">A project name was not specified in the URL.  <router-link :to="{ name: 'clientHome' }">Return Home</router-link></div>
	<FilterList v-else-if="!filterId" :projectName="projectName" />
	<FilterDetails v-else :projectName="projectName" :filterId="parseInt(filterId)" />
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