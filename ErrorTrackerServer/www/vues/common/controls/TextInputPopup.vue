<template>
	<div :class="{ inputDialogRoot: true, shake: shake }" ref="dialogRoot" role="dialog" aria-labelledby="textInputMsgTitle" aria-describedby="textInputMsg">
		<div v-if="title" class="titleBar">
			<div id="textInputMsgTitle" class="title" role="alert">{{title}}</div>
		</div>
		<div class="dialogBody">
			<div id="textInputMsg" class="messageText" v-if="message">{{message}}</div>
			<div class="inputWrapper">
				<input type="text" ref="inputText" :placeholder="placeholder" v-model:value="value" @keypress.enter="okClicked" v-bind:maxlength="maxTextLength" class="textInput" />
			</div>
			<div class="inputWrapper checkbox" v-if="checkboxText">
				<input :id="_uid + 'cb'" type="checkbox" v-model="checked" class="textInput" /><label :for="_uid + 'cb'">{{checkboxText}}</label>
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
			placeholder: {
				type: String,
				default: ""
			},
			initialText: {
				type: String,
				default: ""
			},
			maxTextLength: {
				type: Number,
				default: 4096
			},
			checkboxText: {  // If not null, a checkbox will appear labeled with this text.  If not null, the returned value from this dialog becomes an object containing a string and a bool, rather than just a string.
				type: String,
				default: null
			}
		},
		data()
		{
			return {
				value: "",
				shake: false,
				checked: false
			};
		},
		mounted()
		{
			this.value = " ";
			this.$nextTick(() =>
			{
				// Necessary to work around a glitch where iOS 10 displays the placeholder and the text on top of eachother.
				if (this.initialText)
					this.value = this.initialText;
				else
					this.value = "";

				if (this.$refs.inputText)
					this.$refs.inputText.focus();
				this.$nextTick(() =>
				{
					if (this.$refs.inputText)
						this.$refs.inputText.setSelectionRange(0, this.$refs.inputText.value.length); // select all text so the user can simply begin typing
				});
			});
		},
		methods:
		{
			SetFocus()
			{
				if (this.$refs.inputText)
					this.$refs.inputText.focus();
			},
			DefaultClose()
			{
			},
			okClicked()
			{
				if (this.value)
				{
					if (this.checkboxText)
						this.$emit("close", { value: this.value, checked: this.checked });
					else
						this.$emit("close", { value: this.value });
				}
				else
				{
					this.startShaking();
				}
			},
			cancelClicked()
			{
				this.$emit("close");
			},
			startShaking()
			{
				if (this.shake)
					return;
				animationEvents.forEach(event =>
				{
					if (this.$refs.dialogRoot)
						this.$refs.dialogRoot.addEventListener(event, this.animationEnd);
				});
				this.shake = true;
			},
			animationEnd(e)
			{
				this.shake = false;
				animationEvents.forEach(event =>
				{
					if (this.$refs.dialogRoot)
						this.$refs.dialogRoot.removeEventListener(event, this.animationEnd);
				});
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

	.shake .textInput
	{
		outline: 3px solid #FF0000;
	}

	.textInput
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

	.shake
	{
		-webkit-animation: kf_shake 0.4s 1 linear;
		-moz-animation: kf_shake 0.4s 1 linear;
		-o-animation: kf_shake 0.4s 1 linear;
		animation: kf_shake 0.4s linear 0 1;
	}

	@-webkit-keyframes kf_shake
	{
		0%
		{
			-webkit-transform: translate(30px);
		}

		20%
		{
			-webkit-transform: translate(-30px);
		}

		40%
		{
			-webkit-transform: translate(15px);
		}

		60%
		{
			-webkit-transform: translate(-15px);
		}

		80%
		{
			-webkit-transform: translate(8px);
		}

		100%
		{
			-webkit-transform: translate(0px);
		}
	}

	@-moz-keyframes kf_shake
	{
		0%
		{
			-moz-transform: translate(30px);
		}

		20%
		{
			-moz-transform: translate(-30px);
		}

		40%
		{
			-moz-transform: translate(15px);
		}

		60%
		{
			-moz-transform: translate(-15px);
		}

		80%
		{
			-moz-transform: translate(8px);
		}

		100%
		{
			-moz-transform: translate(0px);
		}
	}

	@-o-keyframes kf_shake
	{
		0%
		{
			-o-transform: translate(30px);
		}

		20%
		{
			-o-transform: translate(-30px);
		}

		40%
		{
			-o-transform: translate(15px);
		}

		60%
		{
			-o-transform: translate(-15px);
		}

		80%
		{
			-o-transform: translate(8px);
		}

		100%
		{
			-o-origin-transform: translate(0px);
		}
	}

	@keyframes kf_shake
	{
		0%
		{
			transform: translate(30px);
		}

		20%
		{
			transform: translate(-30px);
		}

		40%
		{
			transform: translate(15px);
		}

		60%
		{
			transform: translate(-15px);
		}

		80%
		{
			transform: translate(8px);
		}

		100%
		{
			transform: translate(0px);
		}
	}
</style>