<template>
	<div class="changePassword">
		<h2>Change Password</h2>
		<p>User: <b>{{userName}}</b></p>
		<div class="inputs">
			<div class="oldPasswordBox">
				<input class="tb" type="password" name="oldPw" aria-label="Enter Old Password Here" v-model="oldPw" placeholder="Old Password…" autocomplete="current-password" />
			</div>
			<div class="newPasswordBox">
				<input class="tb" type="password" name="newPw" aria-label="Enter New Password Here" v-model="newPw" placeholder="New Password…" autocomplete="new-password" />
			</div>
			<div class="confirmNewPasswordBox">
				<input class="tb" type="password" name="confirmNewPw" aria-label="Confirm New Password Here" v-model="confirmNewPw" placeholder="Confirm New Password…" autocomplete="new-password" />
			</div>
			<div class="changePasswordBox">
				<input type="button" class="changePasswordButton" value="Submit" :disabled="!enabled" @click="TryChangePassword" @keyup.space.enter="TryChangePassword" />
			</div>
		</div>
	</div>
</template>

<script>

	export default {
		components: {},
		props:
		{
		},
		data()
		{
			return {
				oldPw: "",
				newPw: "",
				confirmNewPw: "",
				enabled: true
			};
		},
		created()
		{
		},
		computed:
		{
			userName()
			{
				return this.$store.state.userName;
			}
		},
		methods:
		{
			TryChangePassword()
			{
				if (!this.enabled)
					return;
				if (this.oldPw && this.newPw && this.confirmNewPw)
				{
					if (this.newPw === this.confirmNewPw)
					{
						this.enabled = false;
						this.$store.dispatch("ChangePassword", { oldPw: this.oldPw, newPw: this.newPw })
							.then(data =>
							{
								toaster.success("Password Changed");
								this.enabled = true;
							})
							.catch(err =>
							{
								toaster.error(err);
								this.enabled = true;
							});
					}
					else
						toaster.error('"New Password" does not match "Confirm New Password"');
				}
				else
					toaster.error("Please fill all input fields.");
			}
		},
		watch:
		{
		}
	}
</script>

<style scoped>
	.changePassword
	{
		margin: 20px;
	}

	.oldPasswordBox,
	.newPasswordBox,
	.confirmNewPasswordBox
	{
		margin: 10px 0px;
	}


	.tb
	{
		border-radius: 5px;
		padding: 13px 20px 13px 20px;
		border: 1px solid #CECFCE;
		width: 240px;
		font-size: 18px;
	}

	.changePasswordBox
	{
		margin-top: 15px;
	}

	.changePasswordButton
	{
		font-size: 18px;
		padding: 5px 10px;
	}
</style>