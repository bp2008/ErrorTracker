<template>
	<div class="geolocateIpDialogRoot" role="dialog" aria-labelledby="geolocateIpTitle">
		<div class="titleBar">
			<div id="geolocateIpTitle" class="title" role="alert">Geolocation Information for {{ip}}</div>
		</div>
		<div class="dialogBody">
			<GeolocationEmbed :ip="ip" class="embedded" />
			<div class="buttons">
				<div ref="closeBtn" class="dialogButton" tabindex="0" @click="DefaultClose" @keydown.space.enter.prevent="DefaultClose">
					Close
				</div>
			</div>
		</div>
	</div>
</template>

<script>
	import GeolocationEmbed from 'appRoot/vues/common/controls/GeolocationEmbed.vue';

	export default {
		components: { GeolocationEmbed },
		props:
		{
			ip: {
				type: String,
				default: ""
			}
		},
		methods:
		{
			SetFocus()
			{
				if (this.$refs.closeBtn)
					this.$refs.closeBtn.focus();
			},
			DefaultClose()
			{
				this.$emit("close");
			}
		}
	}
</script>

<style scoped>
	.geolocateIpDialogRoot
	{
		max-width: 600px;
		background-color: #FFFFFF;
	}

	.titleBar
	{
		background-color: #FFFFFF;
		padding: 8px 14px;
		box-sizing: border-box;
	}

	.title
	{
		text-align: center;
		color: black;
		font-weight: bold;
		font-size: 16pt;
	}

	.dialogBody
	{
		padding: 0px 5px 0px 5px;
	}

	.buttons
	{
		display: flex;
	}

	.dialogButton
	{
		display: inline-block;
		cursor: pointer;
		color: #4A9E42;
		font-weight: bold;
		font-size: 12pt;
		box-sizing: border-box;
		position: relative;
		padding: 12px 5px;
		flex: 1 0 auto;
		text-align: center;
	}

		.dialogButton:hover
		{
			background-color: rgba(0,0,0,0.05);
		}

	.embedded
	{
		margin: 10px 5px;
	}
</style>