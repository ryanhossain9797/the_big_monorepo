use anyhow::{Context, Result};
use csv::Reader;
use snowflake_api::{QueryResult, SnowflakeApi};
use std::collections::HashMap;
use std::fs::File;

async fn run_query(sql: &str) -> Result<QueryResult> {
    let api = SnowflakeApi::with_certificate_auth(
        "YCJYUUD-HZ34100",
        None,
        None,
        None,
        "PROTOCOL_AGENT",
        None,
        include_str!("../rsa_key.protocol.p8"),
    )?;
    let res = api.exec(sql).await?;

    Ok(res)
}

fn get_sql_type_for_value(value: &str) -> String {
    if value.parse::<i64>().is_ok() {
        "INTEGER".to_string()
    } else if value.parse::<f64>().is_ok() {
        "FLOAT".to_string()
    } else if value.to_lowercase() == "true" || value.to_lowercase() == "false" {
        "BOOLEAN".to_string()
    } else {
        "STRING".to_string()
    }
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    // Define table identifier at the beginning
    let table_identifier = "UNIVERSE.PK.ABTESTCLUSTER";
    let string_text = "STRING".to_string();
    let csv_path = "./minimal_cluster_info.csv";

    // Open and read the CSV file
    let file =
        File::open(csv_path).with_context(|| format!("Failed to open CSV file: {}", csv_path))?;
    let mut reader = Reader::from_reader(file);

    // Get column headers
    let headers = reader.headers()?.clone();

    // Attempt to infer column types by checking the first row
    let mut column_types = HashMap::new();
    if let Some(result) = reader.records().next() {
        let record = result?;
        for (i, header) in headers.iter().enumerate() {
            let value = record.get(i).unwrap_or_default().to_string();
            column_types.insert(header.to_string(), get_sql_type_for_value(&value));
        }
    } else {
        return Err(anyhow::anyhow!("CSV file is empty or has no data rows"));
    }

    // Create table SQL statement
    let mut create_table_sql = format!("CREATE OR REPLACE TABLE {} (", table_identifier);

    for (i, header) in headers.iter().enumerate() {
        let column_type = column_types.get(header).unwrap_or(&string_text);
        create_table_sql.push_str(&format!("{} {}", header, column_type));

        if i < headers.len() - 1 {
            create_table_sql.push_str(", ");
        }
    }
    create_table_sql.push_str(")");

    // Execute create table query
    println!("Creating table with schema: {}", create_table_sql);
    run_query(&create_table_sql).await?;

    // Re-open CSV file for data insertion
    let file = File::open(csv_path)?;
    let mut reader = Reader::from_reader(file);

    // Prepare and execute insert statements - batch in groups of 100 for efficiency
    let mut row_count = 0;
    let mut batch_size = 0;
    let mut insert_sql = format!("INSERT INTO {} VALUES ", table_identifier);

    for result in reader.records() {
        let record = result?;

        if batch_size > 0 {
            insert_sql.push_str(", ");
        }

        insert_sql.push('(');
        for (i, field) in record.iter().enumerate() {
            let column_type = column_types
                .get(headers.get(i).unwrap_or_default())
                .unwrap_or(&string_text);

            if *column_type == "STRING" {
                insert_sql.push_str(&format!("'{}'", field.replace("'", "''")));
            } else {
                insert_sql.push_str(field);
            }

            if i < record.len() - 1 {
                insert_sql.push_str(", ");
            }
        }
        insert_sql.push(')');

        batch_size += 1;
        row_count += 1;

        // Execute in batches of 100 rows
        if batch_size >= 100 {
            println!("Inserting batch of {} rows...", batch_size);
            run_query(&insert_sql).await?;

            insert_sql = format!("INSERT INTO {} VALUES ", table_identifier);
            batch_size = 0;
        }
    }

    // Insert any remaining rows
    if batch_size > 0 {
        println!("Inserting final batch of {} rows...", batch_size);
        run_query(&insert_sql).await?;
    }

    println!(
        "Successfully created table and inserted {} rows from CSV file",
        row_count
    );

    Ok(())
}
