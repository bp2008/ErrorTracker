﻿<template>
	<div class="loginRoot">
		<div class="loginPanel">
			<div class="systemName">{{systemName}}</div>
			<input type="text" class="txtUser" v-model="user" placeholder="User Name" v-on:keyup.enter="UserNameEnter" autocomplete="username" />
			<input type="password" class="txtPass" v-model="pass" placeholder="Password" v-on:keyup.enter="TryLogin" ref="passwordField" autocomplete="current-password" />
			<input type="button" class="btnLogin" value="Log in" :disabled="loggingIn" @click="TryLogin" @keyup.space.enter="TryLogin" />
			<div class="forgotPwLinkWrapper" v-if="emailConfigured">
				<router-link :to="forgotPwRoute" class="forgotPwLink">Forgot Password?</router-link>
			</div>
		</div>
		<div class="error" v-if="error">{{error}}</div>
	</div>
</template>
<script>
	import { ProgressDialog } from 'appRoot/scripts/ModalDialog';

	export default {
		components: {},
		data()
		{
			return {
				systemName: appContext.systemName,
				user: "",
				pass: "",
				loggingIn: false,
				error: null,
				emailConfigured: appContext.emailConfigured
			};
		},
		computed:
		{
			forgotPwRoute()
			{
				return { name: "forgotPassword", query: { u: this.user } };
			}
		},
		methods:
		{
			TryLogin()
			{
				if (this.loggingIn)
					return;
				this.loggingIn = true;
				this.error = null;
				let progressDialog = ProgressDialog("Logging in…");
				this.$store.dispatch("Login", { user: this.user, pass: this.pass })
					.then(data =>
					{
						// After login completion, don't just push a route. We must actually navigate (load a new browser page) otherwise some bugs can occur like lastpass leaving its icons on screen.
						if (this.$route.query.path)
							location.href = this.$router.resolve({ path: this.$route.query.path }).href;
						else
							location.href = this.$router.resolve({ name: "clientHome" }).href;
					})
					.catch(err =>
					{
						this.error = err;
					})
					.finally(() =>
					{
						this.loggingIn = false;
						progressDialog.close();
					});
			},
			UserNameEnter()
			{
				if (this.$refs.passwordField)
				{
					this.$refs.passwordField.focus();
					if (this.pass)
					{
						this.$refs.passwordField.setSelectionRange(this.pass.length, this.pass.length);
						this.TryLogin();
					}
				}
			}
		}
	}
</script>
<style scoped>
	.loginPanel
	{
		padding: 20px 20px;
		background-color: rgba(255,255,255,0.25);
		border: 1px solid rgba(0,0,0,1);
		border-radius: 8px;
		box-shadow: 0 0 16px rgba(0,0,0,0.5);
		backdrop-filter: blur(8px);
		max-width: 200px;
	}

	.systemName, .txtUser, .txtPass, .btnLogin
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

	.txtUser, .txtPass, .btnLogin
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

	.txtPass
	{
		border-top-width: 0px;
		border-top-left-radius: 0px;
		border-top-right-radius: 0px;
	}

	.btnLogin
	{
		margin-top: 10px;
		cursor: pointer;
		background-image: linear-gradient(to top, #e6e9f0 0%, #eef1f5 100%);
	}

		.btnLogin:hover
		{
			background-image: linear-gradient(to top, #f0f0f3 0%, #f3f7fb 100%);
		}

		.btnLogin:active
		{
			background-image: linear-gradient(to top, #ffffff 0%, #ffffff 100%);
		}

	.error
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

	.forgotPwLinkWrapper
	{
		text-align: center;
		margin-top: 10px;
		font-size: 10pt;
	}
</style>
