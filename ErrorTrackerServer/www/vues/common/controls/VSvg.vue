<template>
	<svg v-on="$listeners" class="vsvg"><use :xlink:href="'#' + sprite"></use><title v-if="title" role="presentation">{{title}}</title></svg>
</template>

<script>
	export default {
		props:
		{
			sprite: {
				type: String,
				required: true
			},
			title: {
				type: String,
				default: ""
			}
		}
	}
</script>