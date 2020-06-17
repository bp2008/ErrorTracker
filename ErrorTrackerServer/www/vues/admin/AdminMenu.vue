<template>
	<nav id="topNav" class="adminNav">
		<router-link :to="{name: 'adminHome' }" class="systemName">{{systemName}} Admin</router-link>
		<router-link :to="{name: 'adminSettings' }" class="adminSettings">Settings</router-link>
		<router-link :to="{name: 'adminUsers' }" class="adminUsers">Users</router-link>
		<router-link :to="{name: 'adminProjects' }" class="adminProjects">Projects</router-link>
		<router-link :to="{name: 'clientHome' }" class="clientHome">Client</router-link>
		<a role="button" @click="logoutClicked" class="logoutButton">Log Out</a>
	</nav>
</template>

<script>
	import ExecAPI from 'appRoot/api/api.js';
	export default {
		data: function ()
		{
			return {
				systemName: appContext.systemName
			};
		},
		computed:
		{
		},
		methods:
		{
			logoutClicked()
			{
				this.$store.dispatch("Logout")
					.then(data =>
					{
						this.$router.push({ name: "login" });
					})
					.catch(err =>
					{
						toaster.error(err);
					});
			}
		}
	}
</script>

<style scoped>
	.adminNav
	{
		position: sticky;
		top: 0px;
		display: flex;
		align-items: center;
		justify-content: space-between;
		flex-wrap: wrap;
		flex-direction: row;
		background-color: #FFFFFF;
		border-bottom: 1px solid #AAAAAA;
	}

		.adminNav *
		{
			padding: 10px 20px;
			cursor: pointer;
			font-size: 20px;
			color: #000000;
			text-decoration: none;
			flex: 1 1 auto;
			text-align: center;
			background-color: #E8E8E8;
			border-left: 1px solid #AAAAAA;
			border-top: 1px solid #AAAAAA;
			margin-top: -1px;
			margin-left: -1px;
		}

			.adminNav *:hover
			{
				background-color: #F2F2F2;
			}

	.systemName
	{
		background-color: #82bf85;
	}

		.systemName:hover
		{
			background-color: #9bdc9e;
		}

	.logoutButton
	{
		background-color: #ffe4cb;
	}

		.logoutButton:hover
		{
			background-color: #fff6ed;
		}

	.router-link-active
	{
		text-decoration: underline;
	}

	@media (min-width: 600px)
	{
		nav
		{
			flex-direction: row;
		}
	}
</style>