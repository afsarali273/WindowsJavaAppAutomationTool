package com.afsarali.jab.client;

import java.util.List;
import java.util.stream.Collectors;

public final class JavaTableRow {
    private final JavaTable table;
    private final int rowIndex;

    JavaTableRow(JavaTable table, int rowIndex) {
        this.table = table;
        this.rowIndex = rowIndex;
    }

    public int index() {
        return rowIndex;
    }

    public List<JavaElementHandle> cells() {
        return table.cells(rowIndex);
    }

    public JavaElementHandle cell(int columnIndex) {
        return table.cell(rowIndex, columnIndex);
    }

    public JavaElementHandle cell(String columnHeader) {
        return table.cell(rowIndex, columnHeader);
    }

    public String text(int columnIndex) {
        return cell(columnIndex).getText();
    }

    public String text(String columnHeader) {
        return cell(columnHeader).getText();
    }

    public JavaElementHandle click(int columnIndex) {
        return cell(columnIndex).click();
    }

    public JavaElementHandle click(String columnHeader) {
        return cell(columnHeader).click();
    }

    public JavaElementHandle doubleClick(int columnIndex) {
        return cell(columnIndex).doubleClick();
    }

    public JavaElementHandle doubleClick(String columnHeader) {
        return cell(columnHeader).doubleClick();
    }

    public List<String> texts() {
        return cells().stream()
                .map(JavaElementHandle::getText)
                .collect(Collectors.toList());
    }
}
