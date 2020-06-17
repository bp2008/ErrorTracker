<template>
	<nav id="topNav" class="clientNav">
		<router-link :to="{name: 'clientHome' }" class="systemName">{{title}}</router-link>
		<router-link v-if="isAdmin" :to="{name: 'adminHome' }" class="adminHome">Admin</router-link>
		<a role="button" @click="logoutClicked" class="logoutButton">Log Out</a>
	</nav>
</template>

<script>
	import ExecAPI from 'appRoot/api/api.js';
	export default {
		data()
		{
			return {
			};
		},
		computed:
		{
			isAdmin()
			{
				return this.$store.state.isAdmin;
			},
			projectName()
			{
				return this.$route.query.p;
			},
			title()
			{
				let t = appContext.systemName;
				if (this.projectName)
					t += " - " + this.projectName;
				return t;
			}
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
	.clientNav
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

		.clientNav *
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

			.clientNav *:hover
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