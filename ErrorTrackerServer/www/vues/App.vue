<template>
	<div class="appRoot">
		<ModalDialogContainer name="dialogFade" transition-name="dialogFade"></ModalDialogContainer>
		<!-- This element follows the cursor -->
		<div v-show="tooltipHtml" class="cursorAttached" :style="cursorAttachedStyle" v-html="tooltipHtml"></div>
		<router-view></router-view>
	</div>
</template>

<script>
	import ModalDialogContainer from "appRoot/vues/common/controls/ModalDialogContainer.vue";
	import EventBus from 'appRoot/scripts/EventBus';
	import { ServiceWorkerInit } from 'appRoot/scripts/ServiceWorkerInit';

	export default {
		components: { ModalDialogContainer },
		data: function ()
		{
			return {
			};
		},
		computed:
		{
			tooltipHtml()
			{
				return EventBus.tooltipHtml;
			},
			cursorAttachedStyle()
			{
				if (EventBus.tooltipHtml)
					return {
						top: (EventBus.mouseY + 26) + "px",
						left: (EventBus.mouseX + 0) + "px"
					};
				return {};
			}
		},
		methods:
		{
		},
		created()
		{
			ServiceWorkerInit();
		}
	};
</script>

<style scoped>
	@import '~vue-context/dist/css/vue-context.css';
	@import url('../Globals.scss');
	@import '~vue-virtual-scroller/dist/vue-virtual-scroller.css';

	.appRoot
	{
		font-size: 0px;
		height: 100%;
		width: 100%;
	}

	.cursorAttached
	{
		position: absolute;
		border: 1px solid #000000;
		background-color: #FFFFEE;
		white-space: pre-wrap;
		padding: 0px 1px;
		opacity: 0.9;
		z-index: 2999999;
		font-size: 16px;
	}
</style>