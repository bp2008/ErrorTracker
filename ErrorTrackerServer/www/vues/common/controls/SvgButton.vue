<template>
	<svg class="svgbtn" :style="btnStyle" @click="onclick" @keypress.enter.prevent="onclick" role="button" tabindex="0"><use :xlink:href="'#' + sprite"></use><title v-if="title" role="presentation">{{title}}</title></svg>
</template>

<script>
	export default {
		props:
		{
			sprite: {
				type: String,
				required: true
			},
			size: {
				type: Number,
				default: 0
			},
			padding: {
				type: String,
				default: ""
			},
			borderWidth: {
				type: String,
				default: ""
			},
			title: {
				type: String,
				default: ""
			}
		},
		data()
		{
			return {
			};
		},
		computed:
		{
			btnStyle()
			{
				let style = {};
				if (this.size > 4)
					style.width = style.height = this.size + "px";
				if (this.padding)
					style.padding = this.padding;
				if (this.borderWidth)
					style.borderWidth = this.borderWidth;

				return style;
			}
		},
		methods:
		{
			onclick(e)
			{
				this.$emit("click", e);
			}
		},
		created()
		{
		}
	}
</script>

<style scoped>
	.svgbtn
	{
		border: 1px solid #000000;
		padding: 1px;
		box-sizing: border-box;
		cursor: pointer;
	}

		.svgbtn:hover
		{
			background-color: rgba(0,0,0,0.05);
		}

		.svgbtn:active
		{
			background-color: rgba(0,0,0,0.085);
		}
</style>