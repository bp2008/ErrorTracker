<template>
	<svg v-on="$listeners"><use :xlink:href="'#' + sprite"></use></svg>
</template>

<script>
	export default {
		props:
		{
			sprite: {
				type: String,
				required: true
			}
		}
	}
</script>