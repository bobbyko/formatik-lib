#Formatik v0.1 Specifications and Details

Formatik


## Input Assumptions and Restrictions
1. Must be a valid List, CSV, JSON, XML or HTML
2. Must have more 3 or more records.
3. Must contain all the 3 records which are provided in the output example

## Example Assumptions and Restrictions

1. Consist of 3 records
2. Is 1 or 2 dimentional structure - list or a table
3. Does not contain blank/empty values
4. Has a simple separators - string of 1 or more characters
5. Has a minimum of n - 1 separators where n is number of records
6. Has records with the same number of fields / tokens
7. Each field's prefix and suffix is the same across all records
8. Tokens must be unique within the input's record they are found
9. Token transformations are not supported. Values are compared as strings and must appear exactly the same between the input and example
0. Only tokens can be mapped. Field names mapping is not supported
1-. Only full token mapping is supported. Partial values constitute token transformation which is not supported 
12. If the same token value is detectable multiple times in an input record, the shallowest selector (selector separators are comma, space, right bracket) selector is selected first. Second selection order for duplicate selectors is alhabetical.
13. separators cannot be substrings of each other

## Output Structure Template

```
{header}
	<record1>
		{prefix1}{token1}{suffix1}{separator2}
		{prefix2}{token2}{suffix2}{separator2}
		...
		{prefixN}{tokenN}{suffixN}
	<record1>
	{separator1}
	<record2>
		{prefix1}{token1}{suffix1}{separator2}
		{prefix2}{token2}{suffix2}{separator2}
		...
		{prefixN}{tokenN}{suffixN}
	<record2>
	{separator1}
	...
	<recordN>
		{prefix1}{token1}{filler1}{separator2}
		{prefix2}{token2}{filler2}{separator2}
		...
		{prefixN}{tokenN}{fillerN}
	<recordN>
{footer}
```

## Process steps
1. Split input into records based on input format. XLS, JSON, CSV, List are supported
2. Extract all tokens from all records, recursivly
3. Exclude tokens that do not exsist as substrings in the example
4. Exclude tokens by bundling them into non-overlapping groups and excluding the ones that do not conform to even table form (equal values per record) and cardinality