<template>
	<router-link v-if="to" :to="to" class="link" replace><slot /></router-link>
	<a v-else="href" role="button" @click="clickFn" class="link"><slot /></a>
</template>

<script>
	// This component exists either as a <router-link> or as an <a role="button" @click="clickFn">, depending on provided props.
	export default {
		props:
		{
			to: {
				default: null
			},
			clickFn: {
				type: Function,
				default: () => { }
			}
		}
	}
</script>
<style scoped>
	.link
	{
		text-decoration: none;
		color: inherit;
	}
</style>