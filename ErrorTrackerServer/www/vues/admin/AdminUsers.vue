<template>
	<div class="pageRoot">
		<div v-if="(error)" class="error">{{error}}</div>
		<div v-else-if="loading" class="loading"><ScaleLoader /> Loading…</div>
		<div v-else-if="users" class="">
			<vue-good-table :columns="columns" :rows="users" @on-row-click="rowClick" />
			<div class="buttonRow">
				<input type="button" value="Add User" @click="addUser" />
			</div>
			<div class="heading">Login History</div>
			<div class="bodySection">
				<LoginRecordsTable :allUsers="true" />
			</div>
		</div>
	</div>
</template>

<script>
	import { GetUserData } from 'appRoot/api/UserData';
	import { ModalDialog } from 'appRoot/scripts/ModalDialog';
	import EditUser from 'appRoot/vues/admin/controls/EditUser.vue';
	import LoginRecordsTable from 'appRoot/vues/common/loginrecords/LoginRecordsTable.vue';

	export default {
		components: { LoginRecordsTable },
		data()
		{
			return {
				error: null,
				loading: false,
				columns: [
					{ label: "User Name", field: "Name" },
					{ label: "Email", field: "Email" },
					{ label: "Is Admin", field: "IsAdmin", type: "boolean" }
				],
				users: null,
				editSpec: null
			};
		},
		created()
		{
			this.loadUsers();
		},
		computed:
		{
		},
		methods:
		{
			loadUsers()
			{
				this.loading = true;
				this.users = null;
				this.editSpec = null;
				GetUserData()
					.then(data =>
					{
						if (data.success)
						{
							this.users = data.users;
							this.editSpec = data.editSpec;
						}
						else
							this.error = data.error;
					})
					.catch(err =>
					{
						this.error = err.message;
					})
					.finally(() =>
					{
						this.loading = false;
					});
			},
			rowClick(params)
			{
				if (this.users)
					for (let i = 0; i < this.users.length; i++)
						if (this.users[i].Name === params.row.Name)
						{
							ModalDialog(EditUser, { user: this.users[i], editSpec: this.editSpec, isAddingUser: false })
								.then(result =>
								{
									if (result)
										this.loadUsers();
								});
						}
			},
			addUser()
			{
				ModalDialog(EditUser, { user: {}, editSpec: this.editSpec, isAddingUser: true })
					.then(result =>
					{
						if (result)
							this.loadUsers();
					});
			}
		}
	}
</script>

<style scoped>
	.pageRoot
	{
		margin: 8px;
	}

	.loading
	{
		margin-top: 80px;
		text-align: center;
	}

	.error
	{
		color: #FF0000;
		font-weight: bold;
	}

	.buttonRow
	{
		margin-top: 30px;
		border-top: 1px solid #AAAAAA;
		padding-top: 10px;
	}

	.heading
	{
		font-size: 20px;
		border-bottom: 1px solid #000000;
		margin-bottom: 10px;
		margin-top: 30px;
		max-width: 400px;
	}

		.heading:first-child
		{
			margin-top: 0px;
		}

	.bodySection
	{
		margin: 0px 20px;
	}
</style>