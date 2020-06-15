<template>
	<div class="prop">
		<label class="propLabel" :for="inputId" v-html="spec.labelHtml" v-if="spec.inputType !== 'checkbox'">
		</label><span v-if="spec.inputType === 'range'">: {{myValue}}</span>
		<div v-if="spec.inputType === 'select'">
			<select v-model="myValue" @change="onChange">
				<option v-for="v in spec.enumValues" :key="v">{{v}}</option>
			</select>
		</div>
		<div v-else-if="spec.inputType === 'array'" class="arrayEditor">
			<div class="arrayElement" v-for="(item, index) in arrayValues" :key="index">
				<select v-if="spec.allowedValues" v-model="item.value" @change="onChange">
					<option v-for="v in spec.allowedValues" :key="v">{{v}}</option>
				</select>
				<input v-else type="spec.arrayType" v-model="item.value" @change="onChange" />
				<input type="button" value="-" @click="removeArrayItem(index)" />
			</div>
			<div>
				<input type="button" value="+" @click="addArrayItem" />
			</div>
		</div>
		<template v-else>
			<input class="propInput" :id="inputId" ref="inputEle" v-model="myValue" :type="spec.inputType" :min="spec.min" :max="spec.max" @change="onChange" @dblclick="onDoubleClick" />
			<label class="checkboxLabel" :for="inputId" v-html="spec.labelHtml" v-if="spec.inputType === 'checkbox'">
			</label>
		</template>
	</div>
</template>

<script>

	export default {
		components: {},
		props:
		{
			initialValue: {
				required: true
			},
			spec: {
				type: Object, // An object containing the item key, default value, and other metadata.
				required: true
			}
		},
		data()
		{
			return {
				myUid: GetUid(),
				myValue: this.initialValue,
				arrayValues: null
			};
		},
		created()
		{
			if (this.spec.inputType === "array")
			{
				let arr = [];
				for (let i = 0; i < this.initialValue.length; i++)
					arr.push({ value: this.initialValue[i] }); // array values must be wrapped in an object to be compatible with v-model nested in v-for
				this.arrayValues = arr;
			}
		},
		mounted()
		{
		},
		computed:
		{
			inputId()
			{
				return "input_" + this.myUid;
			},
			myValueTypeEnforced()
			{
				if (this.spec.inputType === "number" || this.spec.inputType === "range")
					return new Number(this.myValue.toString()).valueOf();
				else if (this.spec.inputType === "array")
					return this.arrayValues.map(v => v.value);
				else
					return this.myValue;
			}
		},
		methods:
		{
			onChange(e)
			{
				this.$emit("valueChanged", this.spec.key, this.myValueTypeEnforced);
			},
			onDoubleClick(e)
			{
				this.myValue = this.spec.value;
				this.onChange(e);
			},
			addArrayItem()
			{
				if (this.spec.allowedValues && this.spec.allowedValues.length > 0)
					this.arrayValues.push({
						value: this.spec.allowedValues[0]
					});
				else
					this.arrayValues.push({
						value: null
					});
				this.onChange();
			},
			removeArrayItem(index)
			{
				this.arrayValues.splice(index, 1);
				this.onChange();
			},
			focus() // Called by parent component
			{
				if (this.$refs.inputEle)
					this.$refs.inputEle.focus();
			}
		},
		beforeDestroy()
		{
		}
	};
</script>

<style scoped>

	.propLabel
	{
		display: block;
		margin-bottom: 1px;
	}

	input[type="checkbox"]
	{
		margin-left: 5px;
	}

	.checkboxLabel
	{
		user-select: none;
	}

	input[type="text"],
	input[type="number"],
	input[type="range"],
	input[type="color"]
	{
		display: block;
		width: 100%;
		box-sizing: border-box;
	}
</style>