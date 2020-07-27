<template>
	<div class="geolocationEmbed">
		<div v-if="error">{{error}}</div>
		<div v-if="loading" class="loading"><ScaleLoader /> Loading…</div>
		<div v-else v-html="html">
		</div>
	</div>
</template>

<script>
	import { GeolocateIP } from 'appRoot/api/LoginRecordData';

	export default {
		components: {},
		props:
		{
			ip: {
				type: String,
				default: ""
			}
		},
		data()
		{
			return {
				error: null,
				loading: false,
				html: "[Not Loaded]"
			};
		},
		created()
		{
			this.loadEmbedHtml();
		},
		methods:
		{
			loadEmbedHtml()
			{
				this.loading = true;
				this.error = null;
				this.html = null;
				GeolocateIP(this.ip)
					.then(data =>
					{
						this.loading = false;
						if (data.success)
							this.html = data.html;
						else
							this.error = data.error;
					})
					.catch(err =>
					{
						this.loading = false;
						this.error = err.message;
					});

			}
		},
		watch:
		{
			ip()
			{
				this.loadEmbedHtml();
			}
		}
	};
</script>

<style scoped>
	.loading
	{
		margin: 20px;
		text-align: center;
	}
</style>