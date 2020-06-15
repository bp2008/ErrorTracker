<template>
	<div class="root">
		<div class="head">
			<ClientMenu></ClientMenu>
		</div>
		<div class="body">
			<router-view></router-view>
		</div>
		<Footer />
	</div>
</template>

<script>

	import ClientMenu from 'appRoot/vues/client/ClientMenu.vue';
	import Footer from 'appRoot/vues/common/Footer.vue';
	import EventBus from 'appRoot/scripts/EventBus';

	export default {
		components: { ClientMenu, Footer },
		data: function ()
		{
			return {
			};
		},
		created()
		{
		},
		mounted()
		{
			EventBus.OnResize();
		},
		computed:
		{
		},
		methods:
		{
		}
	};
</script>

<style scoped>
	.root
	{
		font-size: 16px;
		display: flex;
		flex-direction: column;
		align-items: center;
		width: 100%;
		height: 100%;
	}

	.head
	{
		width: 100%;
	}

	.body
	{
		width: 100%;
	}
</style>