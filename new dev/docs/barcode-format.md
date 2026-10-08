# Barcode Format

Current labels use text barcodes in the form `ItemCode(3 digits) + DDMMYY + 6-digit serial`, for example `153071125004154`. Item codes are exactly three digits and range from 101 through 999. Legacy 9-digit and 13-digit values remain text and must not be parsed as integers.
