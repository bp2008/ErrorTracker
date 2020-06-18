<script>
	// This component can be used just like a <style> element, except within another Vue component's template, allowing it to access reactive props and data!
	/* Example:
	<VStyle>
		body
		{
			background-color: {{bgColor}};
		}
	</VStyle>
	*/
	export default {
		render: function (createElement)
		{
			return createElement('style', this.$slots.default)
		}
	}
</script>