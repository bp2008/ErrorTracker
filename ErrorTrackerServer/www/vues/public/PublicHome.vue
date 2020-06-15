<template>
	<div class="homeRoot">
	</div>
</template>

<script>

	export default {
		data()
		{
			return {
			};
		},
		computed:
		{
		},
		methods:
		{
			//GetHelp()
			//{
			//	toaster.error("Error", "Get Help is not implemented");
			//}
		},
		mounted()
		{
			this.$router.push({ name: "login" });
		}
	}
</script>

<style scoped>
	.homeRoot
	{
		width: 100%;
		text-align: center;
	}

	.welcome
	{
		font-size: 24px;
	}
</style>