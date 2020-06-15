<template>
	<footer id="appFooter">Error Tracker {{serverVersion}}</footer>
</template>

<script>

	export default {
		data: function ()
		{
			return {
				serverVersion: appContext.serverVersion
			};
		}
	}
</script>

<style scoped>
	footer
	{
		margin-top: auto;
		padding: 10px;
		width: 100%;
		text-align: center;
		box-sizing: border-box;
		/*background-color: rgba(255,255,255,0.25);*/
		/*box-shadow: 0 0 16px rgba(0,0,0,0.25);*/
		border-top: 1px solid rgba(0,0,0,0.25);
	}
</style>