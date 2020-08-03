<template>
	<div :class="loginLayoutClasses">
		<router-view class="loginView"></router-view>
		<Footer class="loginFooter" />
		<VStyle>
			.loginLayout.wallpaper
			{
			background-image: url("{{wallpaperUrl}}");
			}
		</VStyle>
	</div>
</template>
<script>
	import Footer from 'appRoot/vues/common/Footer.vue';
	import VStyle from 'appRoot/vues/common/controls/VStyle.vue';

	export default {
		components: { VStyle, Footer },
		data()
		{
			return {
				loginLayoutClasses: ["loginLayout"],
				wallpaperUrl: appContext.appPath + "Wallpaper"
			};
		},
		created()
		{
			if (appContext.loginStyle)
				this.loginLayoutClasses.push(appContext.loginStyle);
		}
	}
</script>
<style scoped>
	.loginView
	{
		margin-top: auto;
		display: flex;
		flex-direction: column;
		align-items: center;
		width: 100%;
	}

	.loginLayout
	{
		font-size: 16px;
		display: flex;
		flex-direction: column;
		justify-content: space-between;
		align-items: center;
		width: 100%;
		height: 100%;
		background-color: #555555;
		background-image: linear-gradient(120deg, #a1c4fd 0%, #c2e9fb 100%);
	}

		.loginLayout.style2
		{
			background-image: linear-gradient(120deg, #84fab0 0%, #8fd3f4 100%);
		}

		.loginLayout.wallpaper
		{
			background-size: cover;
			background-position: center;
		}

	.loginFooter
	{
		background-color: rgba(255,255,255,0.5);
		backdrop-filter: blur(5px);
		box-shadow: 0 0 16px rgba(0,0,0,0.5);
		text-shadow: 0px 0px 4px rgba(255,255,255,1);
	}
</style>
