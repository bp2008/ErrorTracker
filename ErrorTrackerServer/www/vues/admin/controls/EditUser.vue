<template>
	<div class="editUserRoot">
		<div class="title" v-if="isAddingUser">Add New User</div>
		<div class="title" v-if="!isAddingUser">Edit {{originalUserName}}</div>
		<Editor ref="userEditor" :object="editableUser" :spec="editSpec" @valueChanged="valueChanged" />
		<div class="deleteButtonRow" v-if="!isAddingUser">
			<input type="button" value="Delete User" @click="Remove" />
		</div>
		<div class="loginHistoryButtonRow" v-if="!isAddingUser">
			<a :href="loginHistoryLink" target="_blank">Login History <vsvg sprite="open_in_new" class="open_in_new" /></a>
		</div>
		<div class="buttonRow">
			<input v-if="!isAddingUser" type="button" value="Save" @click="Save" />
			<input v-if="isAddingUser" type="button" value="Add User" @click="Add" />
			<input type="button" value="Cancel" @click="Cancel" />
		</div>
	</div>
</template>

<script>
	import Editor from 'appRoot/vues/common/editor/Editor.vue';
	import { SetUserData, AddUser, RemoveUser } from 'appRoot/api/UserData';
	import { ModalConfirmDialog } from 'appRoot/scripts/ModalDialog';
	import svg1 from 'appRoot/images/sprite/open_in_new.svg';

	export default {
		components: { Editor },
		props:
		{
			user: {
				type: Object,
				required: true
			},
			editSpec: {
				type: Array,
				required: true
			},
			isAddingUser: {
				type: Boolean,
				required: true
			}
		},
		data()
		{
			return {
				originalUserName: null,
				editableUser: null
			};
		},
		created()
		{
			this.originalUserName = this.user.Name;
			this.editableUser = JSON.parse(JSON.stringify(this.user));
		},
		mounted()
		{
		},
		computed:
		{
			loginHistoryLink()
			{
				console.log(this.$router.resolve({ name: 'adminUserLoginHistory', params: { userName: this.user.Name } }));
				return this.$router.resolve({ name: 'adminUserLoginHistory', params: { userName: this.user.Name } }).href;
			}
		},
		methods:
		{
			SetFocus()
			{
				this.$refs.userEditor.focusFirst();
			},
			DefaultClose()
			{
				this.Cancel();
			},
			Save()
			{
				SetUserData(this.originalUserName, this.editableUser)
					.then(data =>
					{
						if (data.success)
							this.$emit("close", true);
						else
							toaster.error(data.error);
					})
					.catch(err =>
					{
						toaster.error(err);
					});
			},
			Cancel()
			{
				this.$emit("close", false);
			},
			Add()
			{
				AddUser(this.editableUser)
					.then(data =>
					{
						if (data.success)
							this.$emit("close", true);
						else
							toaster.error(data.error);
					})
					.catch(err =>
					{
						toaster.error(err);
					});
			},
			Remove()
			{
				ModalConfirmDialog("Are you sure you want to delete this user?", "Confirm")
					.then(result =>
					{
						if (result)
						{
							RemoveUser(this.originalUserName)
								.then(data =>
								{
									if (data.success)
										this.$emit("close", true);
									else
										toaster.error(data.error);
								})
								.catch(err =>
								{
									toaster.error(err);
								});
						}
					});
			},
			valueChanged(key, value)
			{
				this.editableUser[key] = value;
			}
		},
		beforeDestroy()
		{
		}
	};
</script>

<style scoped>
	.editUserRoot
	{
		margin: 10px;
		min-width: 260px;
	}

	.title
	{
		border-bottom: 1px solid #000000;
		margin-bottom: 10px;
		font-size: 20px;
	}

	.deleteButtonRow,
	.loginHistoryButtonRow
	{
		margin-top: 20px;
		margin-bottom: 20px;
	}

	.open_in_new
	{
		width: 16px;
		height: 16px;
		fill: currentColor;
		vertical-align: middle;
	}
</style>