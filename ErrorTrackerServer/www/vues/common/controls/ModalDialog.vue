<template>
	<div :class="{ modalDialogRoot: true, positionAbsolute: usePositionAbsolute }"
		 aria-hidden="false"
		 aria-modal="true"
		 tabindex="0"
		 @keyup.esc.prevent="defaultClose"
		 :style="rootStyle">
		<div class="modalDialogOverlay" @mousedown="overlayMouseDown" @click="overlayClick"></div>
		<div class="modalDialogPositioner" :style="dynPosStyle" @mousedown="overlayMouseDown" @click="overlayClick">
			<!-- Needed beause iOS treats position: fixed as position: absolute for these dialogs -->
			<div class="FocusCatcher" tabindex="0" @focus="FocusCatcher(false)"></div>
			<div class="modalDialogContent"
				 :style="dynContentStyle"
				 @mousedown="contentMouseDown"
				 @click="contentClick">
				<component v-bind:is="contentComponent"
						   v-bind="contentProps"
						   ref="contentComponent"
						   @close="closeRequested" />
			</div>
			<div class="FocusCatcher" tabindex="0" @focus="FocusCatcher(true)"></div>
		</div>
	</div>
</template>

<script>
	// All modal dialogs in the app should use this component as a base, to provide easier maintenance and consistent behavior.

	import ModalDialogAccessibilityMixin from 'appRoot/scripts/ModalDialogAccessibilityMixin.js';
	import { BrowserIsIOS, FocusDescendant } from 'appRoot/scripts/Util.js';

	export default {
		mixins: [ModalDialogAccessibilityMixin],
		props:
		{
			contentComponent: {
				type: Object,
				required: true
			},
			contentProps: null,
			zIndex: {
				type: Number,
				default: 0
			},
			positionAbsolute: {
				type: Boolean,
				default: false
			},
			halfHeight: {
				type: Boolean, // If true, the dialog will be centered in the top half of the window instead of the full window.
				default: false
			}
		},
		data()
		{
			return {
				lastMouseDownWasOnOverlay: false,
				absPosTop: 0
			};
		},
		methods:
		{
			contentMouseDown(e)
			{
				e.stopPropagation();
				this.lastMouseDownWasOnOverlay = false;
			},
			contentClick(e)
			{
				e.stopPropagation();
			},
			overlayMouseDown(e)
			{
				this.lastMouseDownWasOnOverlay = true;
			},
			overlayClick(e)
			{
				if (this.lastMouseDownWasOnOverlay)
					this.defaultClose();
			},
			defaultClose()
			{
				if (this.$refs.contentComponent && typeof this.$refs.contentComponent.DefaultClose === "function")
					this.$refs.contentComponent.DefaultClose();
				else
					this.$emit("close", false);
			},
			closeRequested(args)
			{
				if (typeof args === "undefined")
					this.$emit("close", false);
				else
					this.$emit("close", args);
			},
			SetFocus()
			{
				if (!this.$refs.contentComponent)
					return;
				if (typeof this.$refs.contentComponent.SetFocus === "function")
					this.$refs.contentComponent.SetFocus();
				else
				{
					console.error("ModalDialog contentComponent does not have SetFocus function");
					this.FocusCatcher(true);
				}
			},
			FocusCatcher(focusFirstItem)
			{
				FocusDescendant(this.$refs.contentComponent.$el, focusFirstItem);
			}
		},
		computed:
		{
			myRole()
			{
				if (this.type === "alertdialog")
					return "alertdialog";
				return "dialog";
			},
			rootStyle()
			{
				let style = {};
				if (this.zIndex)
					style.zIndex = this.zIndex;
				if (this.usePositionAbsolute && this.absPosTop)
					style.top = this.absPosTop;
				return style;
			},
			isIOS()
			{
				return BrowserIsIOS();
			},
			usePositionAbsolute()
			{
				return this.positionAbsolute || this.isIOS;
			},
			dynContentStyle()
			{
				if (!this.usePositionAbsolute)
					return { maxHeight: "100vh", maxWidth: "100vw" };
				return {};
			},
			dynPosStyle()
			{
				if (this.halfHeight)
					return { height: "50%", minHeight: "50%" };
				else if (this.usePositionAbsolute)
					return { height: "auto" };
				return {};
			}
		},
		created()
		{
			this.absPosTop = window.pageYOffset;
		}
	}
</script>

<style scoped>
	.modalDialogRoot
	{
		position: fixed;
		top: 0px;
		left: 0px;
		width: 100%;
		height: 100%;
		display: flex;
		flex-direction: column;
		justify-content: flex-start;
		align-items: center;
		z-index: 4010;
		font-size: 0px;
	}

	.modalDialogPositioner
	{
		position: absolute;
		top: 0px;
		left: 0px;
		right: 0px;
		min-height: 100%;
		height: 100vh;
		display: flex;
		flex-direction: column;
		justify-content: center;
		align-items: center;
		overflow: visible;
	}

	.modalDialogOverlay
	{
		position: fixed;
		top: 0px;
		left: 0px;
		width: 100%;
		height: 100%;
		background-color: rgba(0, 0, 0, 0.15);
	}

	.modalDialogContent
	{
		position: relative;
		background-color: #FFFFFF;
		border-radius: 5px;
		-webkit-box-shadow: 0px 0px 10px 6px rgba(0, 0, 0, 0.25);
		-moz-box-shadow: 0px 0px 10px 6px rgba(0, 0, 0, 0.25);
		box-shadow: 0px 0px 10px 6px rgba(0, 0, 0, 0.25);
		overflow-y: auto;
	}

		.modalDialogContent > *
		{
			font-size: 12pt;
		}

	.FocusCatcher
	{
		width: 0px;
		height: 0px;
	}

	.positionAbsolute
	{
		position: absolute;
		background-color: transparent;
	}

		.positionAbsolute .modalDialogContent
		{
			margin: 40px 5px;
			max-height: none;
		}

	@media (min-width: 400px)
	{
		.modalDialogContent
		{
			border-radius: 10px;
		}
	}
</style>