<template>
	<div :class="loginRootClasses">
		<div class="loginPanel">
			<div class="systemName">{{systemName}}</div>
			<input type="text" class="txtUser" v-model="user" placeholder="User Name" v-on:keyup.enter="TryLogin" />
			<input type="password" class="txtPass" v-model="pass" placeholder="Password" v-on:keyup.enter="TryLogin" />
			<input type="button" class="btnLogin" value="Log in" :disabled="!loginEnabled" @click="TryLogin" @keyup.space.enter="TryLogin" />
		</div>
		<div class="error" v-if="error">{{error}}</div>
		<Footer class="loginFooter" />
	</div>
</template>
<script>
	import Footer from 'appRoot/vues/common/Footer.vue';

	export default {
		components: { Footer },
		data()
		{
			return {
				systemName: appContext.systemName,
				user: "",
				pass: "",
				loginEnabled: true,
				error: null,
				loginRootClasses: ["loginRoot"]
			};
		},
		created()
		{
			if (appContext.loginStyle)
				this.loginRootClasses.push(appContext.loginStyle);
		},
		computed:
		{
		},
		methods:
		{
			TryLogin()
			{
				if (!this.loginEnabled)
					return;
				this.loginEnabled = false;
				this.error = null;
				this.$store.dispatch("Login", { user: this.user, pass: this.pass })
					.then(data =>
					{
						// After login completion, don't just push a route. We must actually navigate (load a new browser page) otherwise some bugs can occur like lastpass leaving its icons on screen.
						if (this.$route.query.path)
							location.href = this.$router.resolve({ path: this.$route.query.path }).href;
						else if (data.admin)
							location.href = this.$router.resolve({ name: "adminStatus" }).href;
						else
							location.href = this.$router.resolve({ name: "clientHome" }).href;
					})
					.catch(err =>
					{
						this.error = err;
						this.loginEnabled = true;
					});
			}
		}
	}
</script>
<style scoped>
	.loginRoot
	{
		font-size: 16px;
		display: flex;
		flex-direction: column;
		justify-content: space-between;
		align-items: center;
		width: 100%;
		height: 100%;
		background-color: #555555;
		background-image: linear-gradient(120deg, #a1c4fd 0%, #c2e9fb 100%);
	}

		.loginRoot.style2
		{
			background-image: linear-gradient(120deg, #84fab0 0%, #8fd3f4 100%);
		}

		.loginRoot.wallpaper
		{
			background-image: url("/Wallpaper");
			background-size: cover;
			background-position: center;
		}

	.loginPanel
	{
		margin-top: auto;
		padding: 20px 20px;
		background-color: rgba(255,255,255,0.25);
		border: 1px solid rgba(0,0,0,1);
		border-radius: 8px;
		box-shadow: 0 0 16px rgba(0,0,0,0.5);
		backdrop-filter: blur(8px);
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

	.loginFooter
	{
		background-color: rgba(255,255,255,0.5);
		backdrop-filter: blur(5px);
		box-shadow: 0 0 16px rgba(0,0,0,0.5);
		text-shadow: 0px 0px 4px rgba(255,255,255,1);
	}
</style>
