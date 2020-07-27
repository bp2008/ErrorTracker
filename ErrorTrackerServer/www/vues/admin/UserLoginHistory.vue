<template>
	<div class="pageRoot">
		<div class="heading">Login History for {{userName}}</div>
		<div class="bodySection">
			<LoginRecordsTable :userName="userName" />
		</div>
	</div>
</template>

<script>
	import LoginRecordsTable from 'appRoot/vues/common/loginrecords/LoginRecordsTable.vue';

	export default {
		components: { LoginRecordsTable },
		props:
		{
			userName: {
				type: String,
				default: ""
			}
		}
	}
</script>

<style scoped>
	.pageRoot
	{
		margin: 8px;
	}

	.heading
	{
		font-size: 20px;
		border-bottom: 1px solid #000000;
		margin-bottom: 10px;
		margin-top: 30px;
		max-width: 400px;
	}

		.heading:first-child
		{
			margin-top: 0px;
		}

	.bodySection
	{
		margin: 0px 20px;
	}
</style>