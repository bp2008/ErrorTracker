<template>
	<div class="progressDialogRoot" role="dialog" aria-labelledby="loadingTitle">
		<div class="dialogBody">
			<ScaleLoader />
			<div id="loadingTitle" class="title" role="alert">{{text}}</div>
		</div>
	</div>
</template>

<script>
	export default {
		props:
		{
			text: {
				type: String,
				default: "Loading…"
			},
			close: {
				type: Boolean,
				required: true
			}
		},
		methods:
		{
			SetFocus()
			{
			},
			DefaultClose()
			{
			}
		},
		watch:
		{
			close()
			{
				if (this.close)
					this.$emit("close");
			}
		}
	}
</script>

<style scoped>
	.progressDialogRoot
	{
		max-width: 300px;
		background-color: #FFFFFF;
	}

	.dialogBody
	{
		padding: 20px;
	}
</style>