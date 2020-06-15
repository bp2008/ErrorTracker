<template>
	<div :class="{ inputDialogRoot: true }" ref="dialogRoot" role="dialog" aria-labelledby="textInputMsgTitle" aria-describedby="colorInputMsg">
		<div v-if="title" class="titleBar">
			<div id="textInputMsgTitle" class="title" role="alert">{{title}}</div>
		</div>
		<div class="dialogBody">
			<div id="colorInputMsg" class="messageText" v-if="message">{{message}}</div>
			<div class="inputWrapper">
				<input type="color" ref="inputColor" v-model:value="value" @keypress.enter="okClicked" class="colorInput" />
			</div>
			<div class="buttons">
				<div class="dialogButton okButton" tabindex="0" @click="okClicked" @keydown.space.enter.prevent="okClicked">
					OK
				</div>
				<div class="dialogButton cancelButton" tabindex="0" @click="cancelClicked" @keydown.space.enter.prevent="cancelClicked">
					Cancel
				</div>
			</div>
		</div>
	</div>
</template>

<script>
	let animationEvents = ["webkitAnimationEnd", "oanimationend", "msAnimationEnd", "animationend"];

	export default {
		props:
		{
			title: {
				type: String,
				default: null
			},
			message: {
				type: String,
				default: ""
			},
			initialColor: {
				type: String,
				default: "FF0000"
			}
		},
		data()
		{
			return {
				value: "#FF0000",
				checked: false
			};
		},
		created()
		{
			if (this.initialColor && this.initialColor.length === 6)
				this.value = "#" + this.initialColor;
		},
		methods:
		{
			SetFocus()
			{
				if (this.$refs.inputColor)
					this.$refs.inputColor.focus();
			},
			DefaultClose()
			{
			},
			okClicked()
			{
				let color = this.value;
				if (color.indexOf('#') === 0)
					color = color.substr(1);
				this.$emit("close", { value: color });
			},
			cancelClicked()
			{
				this.$emit("close");
			}
		}
	}
</script>

<style scoped>
	.inputDialogRoot
	{
		max-width: 300px;
		background-color: #FFFFFF;
	}

	.checkbox
	{
		font-size: 15px;
		display: flex;
		flex-direction: row;
		justify-content: center;
		margin-bottom: 5px;
	}

		.checkbox label
		{
		}

	.titleBar
	{
		background-color: #FFFFFF;
		padding: 8px 14px;
		box-sizing: border-box;
	}

	.title
	{
		text-align: center;
		color: black;
		font-weight: bold;
		font-size: 16pt;
	}

	.messageText
	{
		margin: 8px 14px;
	}

	.dialogBody
	{
		padding: 0px 5px 0px 5px;
	}

	.inputWrapper
	{
		display: flex;
	}

	.colorInput
	{
		flex: 1 1 auto;
		margin: 10px 15px;
		border: 1px solid #AAAAAA;
		border-radius: 3px;
		padding: 3px 6px;
		font-size: 13pt;
	}

	.buttons
	{
		display: flex;
	}

	.dialogButton
	{
		display: inline-block;
		cursor: pointer;
		color: black;
		font-weight: bold;
		font-size: 12pt;
		box-sizing: border-box;
		position: relative;
		padding: 12px 5px;
		flex: 1 0 auto;
		text-align: center;
	}

		.dialogButton:hover
		{
			background-color: rgba(0,0,0,0.05);
		}

	.okButton
	{
		color: #4A9E42;
	}

	.cancelButton
	{
		color: #CE7D29;
		border-left: 1px solid #DDDDDD;
	}
</style>