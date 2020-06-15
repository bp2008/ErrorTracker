<template>
	<div :class="{ resizeBar: true, vertical: !horizontal, horizontal: horizontal}"
		 :style="dynStyle"
		 @mousedown="mouseDown"
		 @dblclick="dblClick">
	</div>
</template>

<script>
	import EventBus from 'appRoot/scripts/EventBus';
	import { Clamp } from 'appRoot/scripts/Util';

	export default {
		props:
		{
			min: {
				type: Number,
				required: true
			},
			max: {
				type: Number,
				required: true
			},
			start: {
				type: Number,
				required: true
			},
			default: {
				type: Number,
				required: true
			},
			offset: {
				type: Number,
				default: 0
			},
			horizontal: {
				type: Boolean,
				default: false
			}
		},
		data()
		{
			return {
				size: Clamp(this.start, this.min, this.max),
				dragState: {
					active: false,
					pointerOffset: 0
				}
			};
		},
		computed:
		{
			dynStyle()
			{
				if (this.horizontal)
					return {
						top: ((this.size + this.offset) - 5) + "px"
					};
				else
					return {
						left: ((this.size + this.offset) - 5) + "px"
					};
			},
			mouseX()
			{
				return EventBus.mouseX;
			},
			mouseY()
			{
				return EventBus.mouseY;
			},
			mouseUps()
			{
				return EventBus.mouseUps;
			}
		},
		methods:
		{
			mouseDown(e)
			{
				if (this.horizontal)
					this.dragState.pointerOffset = EventBus.mouseY - this.size;
				else
					this.dragState.pointerOffset = EventBus.mouseX - this.size;
				this.dragState.active = true;
			},
			dblClick(e)
			{
				this.size = Clamp(this.default, this.min, this.max);
				this.$emit("change", this.size);
			}
		},
		watch:
		{
			mouseX()
			{
				if (!this.horizontal && this.dragState.active)
				{
					this.size = Clamp(EventBus.mouseX - this.dragState.pointerOffset, this.min, this.max);
					this.$emit("change", this.size);
				}
			},
			mouseY()
			{
				if (this.horizontal && this.dragState.active)
				{
					this.size = Clamp(EventBus.mouseY - this.dragState.pointerOffset, this.min, this.max);
					this.$emit("change", this.size);
				}
			},
			mouseUps()
			{
				this.dragState.active = false;
			}
		}
	}
</script>

<style scoped>
	.resizeBar
	{
		position: absolute;
/*		background-color: rgba(255,0,0,0.25);*/
		user-select: none;
		z-index: 100;
	}

		.resizeBar.vertical
		{
			height: 100%;
			width: 9px;
			top: 0px;
			cursor: ew-resize;
		}

		.resizeBar.horizontal
		{
			width: 100%;
			height: 9px;
			left: 0px;
			cursor: ns-resize;
		}
</style>