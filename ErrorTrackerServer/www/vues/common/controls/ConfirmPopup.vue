<template>
	<div class="confirmRoot" role="alertdialog" aria-labelledby="confirmMsgTitle" aria-describedby="confirmMsgLabel">
		<div v-if="title" class="titleBar">
			<div class="title" id="confirmMsgTitle" role="alert">{{title}}</div>
		</div>
		<div class="dialogBody">
			<div ref="msgLabel" id="confirmMsgLabel" class="messageText" v-bind:style="messageTextStyle" role="document" tabindex="0">{{message}}</div>
			<div class="controls" v-if="showButtons">
				<div ref="btnOK" role="button" v-if="!confirm" class="dialogButton okButton" tabindex="0" @click="$emit('close', true)" @keydown.space.enter.prevent="$emit('close', true)">
					<!-- keyup should not be used here, or the dialog may close immediately when invoked after a keydown event -->
					{{okText}}
				</div>
				<div ref="btnYes" role="button" v-if="confirm" class="dialogButton yesButton" tabindex="0" @click="$emit('close', true)" @keydown.space.enter.prevent="$emit('close', true)" @keydown.right.prevent="FocusNo">
					{{yesText}}
				</div>
				<div ref="btnNo" role="button" v-if="confirm" class="dialogButton noButton" tabindex="0" @click="$emit('close', false)" @keydown.space.enter.prevent="$emit('close', false)" @keydown.left.prevent="FocusYes">
					{{noText}}
				</div>
			</div>
		</div>
	</div>
</template>

<script>

	export default {
		props:
		{
			title: {
				type: String,
				default: null
			},
			message: {
				type: String,
				default: "Message not set"
			},
			yesText: {
				type: String,
				default: "Yes"
			},
			noText: {
				type: String,
				default: "No"
			},
			okText: {
				type: String,
				default: "OK"
			},
			cancelMeansYes: {
				type: Boolean,
				default: false
			},
			confirm: {
				type: Boolean,
				default: false
			},
			showButtons: {
				type: Boolean,
				default: true
			},
			autoClose: {
				type: Number,
				default: 0
			}
		},
		methods:
		{
			SetFocus()
			{
				if (this.showButtons)
				{
					if (this.confirm)
					{
						if (this.cancelMeansYes)
							this.FocusYes();
						else
							this.FocusNo();
					}
					else
						this.$refs.btnOK.focus();
				}
				else
				{
					this.$refs.msgLabel.focus();
				}
			},
			DefaultClose()
			{
				this.$emit('close', this.cancelMeansYes);
			},
			FocusYes()
			{
				this.$refs.btnYes.focus();
			},
			FocusNo()
			{
				this.$refs.btnNo.focus();
			}
		},
		computed:
		{
			messageTextStyle()
			{
				if (this.showButtons)
				{
					return {
						padding: "8px 14px 20px 14px"
					};
				}
				else
				{
					return {
						padding: "8px 14px 8px 14px"
					};
				}
			}
		},
		mounted()
		{
			if (this.autoClose > 0)
			{
				let saveThis = this;
				setTimeout(function ()
				{
					saveThis.$emit('close', true);
				}, this.autoClose * 1000);
			}
		}
	}
</script>

<style scoped>
	.confirmRoot
	{
		max-width: 300px;
		min-width: 200px;
		background-color: #FFFFFF;
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
		padding: 8px 14px 20px 14px;
		white-space: pre-wrap;
	}


	.controls
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

	.yesButton
	{
		color: #4A9E42;
	}

	.noButton
	{
		color: #CE7D29;
		border-left: 1px solid #DDDDDD;
	}
</style>