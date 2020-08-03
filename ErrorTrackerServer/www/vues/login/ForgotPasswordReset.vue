<template>
	<div class="forgotPw">
		<div class="forgotPwPanel" title="Note: Recovery emails have a 5 minute cooldown per user account.">
			<div class="systemName">{{systemName}}</div>
			<p class="titleBar">Password Recovery</p>
			<p class="hint">To reset <b>{{initialUser}}</b>, paste the Security Code that has been emailed to you.</p>
			<input type="text" class="txtUser" v-model="securityCode" placeholder="Security Code" v-on:keyup.enter="DoReset" />
			<input type="button" class="btnRequest" value="Reset Password" :disabled="!enabled" @click="DoReset" @keyup.space.enter="DoReset" />
			<div class="backToLoginLinkWrapper">
				<router-link :to="{ name: 'login' }" class="backToLoginLink">Back to Login</router-link>
			</div>
		</div>
		<div class="error" v-if="error">{{error}}</div>
		<div class="resetCompleted" v-if="resetCompleted">The password for <b>{{initialUser}}</b> has been reset and sent to their email.</div>
	</div>
</template>

<script>
	import ExecAPI from 'appRoot/api/api';

	export default {
		components: {},
		props:
		{
			initialUser: {
				type: String,
				default: ""
			}
		},
		data()
		{
			return {
				systemName: appContext.systemName,
				securityCode: "",
				enabled: true,
				error: null,
				resetCompleted: false
			};
		},
		created()
		{
		},
		methods:
		{
			DoReset()
			{
				if (!this.enabled)
					return;
				this.enabled = false;
				this.error = null;

				return ExecAPI("ForgotPW/Reset", { accountIdentifier: this.initialUser, token: this.securityCode })
					.then(data =>
					{
						this.enabled = true;
						if (data.success)
						{
							this.resetCompleted = true;
							this.enabled = false;
						}
						else
							this.error = data.error;
					})
					.catch(err =>
					{
						this.error = err.message;
						this.enabled = true;
					});
			}
		}
	}
</script>

<style scoped>
	.forgotPwPanel
	{
		padding: 20px 20px;
		background-color: rgba(255,255,255,0.75);
		border: 1px solid rgba(0,0,0,1);
		border-radius: 8px;
		box-shadow: 0 0 16px rgba(0,0,0,0.5);
		backdrop-filter: blur(8px);
		max-width: 200px;
	}

	.titleBar
	{
		text-align: center;
		font-weight: bold;
	}


	.systemName, .txtUser, .btnRequest
	{
		width: 200px;
		box-sizing: border-box;
	}

	.systemName
	{
		font-size: 24px;
		text-shadow: 1px 1px 4px rgba(255,255,255,1);
		margin-bottom: 15px;
		text-align: center;
		overflow-x: hidden;
		word-break: break-word;
		white-space: pre-line;
	}

	.txtUser, .btnRequest
	{
		display: block;
		border: 1px solid gray;
		border-radius: 3px;
		padding: 2px 4px;
	}

	.txtUser
	{
		border-bottom-left-radius: 0px;
		border-bottom-right-radius: 0px;
	}

	.btnRequest
	{
		margin-top: 10px;
		cursor: pointer;
		background-image: linear-gradient(to top, #e6e9f0 0%, #eef1f5 100%);
	}

		.btnRequest:hover
		{
			background-image: linear-gradient(to top, #f0f0f3 0%, #f3f7fb 100%);
		}

		.btnRequest:active
		{
			background-image: linear-gradient(to top, #ffffff 0%, #ffffff 100%);
		}

	.error,
	.resetCompleted
	{
		margin: 20px 10px;
		border: 1px solid #000000;
		border-radius: 5px;
		background-color: rgba(255,180,180,0.75);
		padding: 8px;
		max-width: 600px;
		box-sizing: border-box;
		word-break: break-word;
		white-space: pre-line;
		backdrop-filter: blur(8px);
	}

	.resetCompleted
	{
		background-color: rgba(180,255,180,0.75);
	}

	.backToLoginLinkWrapper
	{
		text-align: center;
		margin-top: 10px;
		font-size: 10pt;
	}
</style>