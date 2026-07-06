package com.afsarali.jab.client;

import java.util.List;

public final class JavaTable {
    private final TableTarget target;

    private JavaTable(TableTarget target) {
        this.target = target;
    }

    static JavaTable from(JavaElement element) {
        return new JavaTable(new SessionElementTarget(element));
    }

    static JavaTable from(JavaObject object) {
        return new JavaTable(new StatelessObjectTarget(object));
    }

    static JavaTable from(JavaElementHandle handle) {
        return new JavaTable(new HandleTarget(handle));
    }

    public List<JavaElementHandle> rows() {
        return target.rows();
    }

    public List<JavaElementHandle> rows(Integer maxResults) {
        return target.rows(maxResults);
    }

    public JavaTableRow row(int rowIndex) {
        return new JavaTableRow(this, rowIndex);
    }

    public List<JavaElementHandle> cells() {
        return target.cells();
    }

    public List<JavaElementHandle> cells(int rowIndex) {
        return target.cells(rowIndex);
    }

    public List<JavaElementHandle> cells(String columnHeader) {
        return target.cells(columnHeader);
    }

    public JavaElementHandle cell(int rowIndex, int columnIndex) {
        return target.cell(rowIndex, columnIndex);
    }

    public JavaElementHandle cell(int rowIndex, String columnHeader) {
        return target.cell(rowIndex, columnHeader);
    }

    public String text(int rowIndex, int columnIndex) {
        return cell(rowIndex, columnIndex).getText();
    }

    public String text(int rowIndex, String columnHeader) {
        return cell(rowIndex, columnHeader).getText();
    }

    public JavaElementHandle click(int rowIndex, int columnIndex) {
        return cell(rowIndex, columnIndex).click();
    }

    public JavaElementHandle click(int rowIndex, String columnHeader) {
        return cell(rowIndex, columnHeader).click();
    }

    public JavaElementHandle doubleClick(int rowIndex, int columnIndex) {
        return cell(rowIndex, columnIndex).doubleClick();
    }

    public JavaElementHandle doubleClick(int rowIndex, String columnHeader) {
        return cell(rowIndex, columnHeader).doubleClick();
    }

    private interface TableTarget {
        List<JavaElementHandle> rows();
        List<JavaElementHandle> rows(Integer maxResults);
        List<JavaElementHandle> cells();
        List<JavaElementHandle> cells(int rowIndex);
        List<JavaElementHandle> cells(String columnHeader);
        JavaElementHandle cell(int rowIndex, int columnIndex);
        JavaElementHandle cell(int rowIndex, String columnHeader);
    }

    private static final class SessionElementTarget implements TableTarget {
        private final JavaElement element;

        private SessionElementTarget(JavaElement element) {
            this.element = element;
        }

        @Override public List<JavaElementHandle> rows() { return element.findTableRows(); }
        @Override public List<JavaElementHandle> rows(Integer maxResults) { return take(rows(), maxResults); }
        @Override public List<JavaElementHandle> cells() { return element.findTableCells(); }
        @Override public List<JavaElementHandle> cells(int rowIndex) { return element.findTableCells(rowIndex); }
        @Override public List<JavaElementHandle> cells(String columnHeader) { return element.findTableCells(columnHeader); }
        @Override public JavaElementHandle cell(int rowIndex, int columnIndex) { return element.findTableCell(rowIndex, columnIndex); }
        @Override public JavaElementHandle cell(int rowIndex, String columnHeader) { return element.findTableCell(rowIndex, columnHeader); }
    }

    private static final class StatelessObjectTarget implements TableTarget {
        private final JavaObject object;

        private StatelessObjectTarget(JavaObject object) {
            this.object = object;
        }

        @Override public List<JavaElementHandle> rows() { return object.findTableRows(); }
        @Override public List<JavaElementHandle> rows(Integer maxResults) { return take(rows(), maxResults); }
        @Override public List<JavaElementHandle> cells() { return object.findTableCells(); }
        @Override public List<JavaElementHandle> cells(int rowIndex) { return object.findTableCells(rowIndex); }
        @Override public List<JavaElementHandle> cells(String columnHeader) { return object.findTableCells(columnHeader); }
        @Override public JavaElementHandle cell(int rowIndex, int columnIndex) { return object.findTableCell(rowIndex, columnIndex); }
        @Override public JavaElementHandle cell(int rowIndex, String columnHeader) { return object.findTableCell(rowIndex, columnHeader); }
    }

    private static final class HandleTarget implements TableTarget {
        private final JavaElementHandle handle;

        private HandleTarget(JavaElementHandle handle) {
            this.handle = handle;
        }

        @Override public List<JavaElementHandle> rows() { return handle.findTableRows(); }
        @Override public List<JavaElementHandle> rows(Integer maxResults) { return handle.findTableRows(maxResults); }
        @Override public List<JavaElementHandle> cells() { return handle.findTableCells(); }
        @Override public List<JavaElementHandle> cells(int rowIndex) { return handle.findTableCells(rowIndex); }
        @Override public List<JavaElementHandle> cells(String columnHeader) { return handle.findTableCells(columnHeader); }
        @Override public JavaElementHandle cell(int rowIndex, int columnIndex) { return handle.findTableCell(rowIndex, columnIndex); }
        @Override public JavaElementHandle cell(int rowIndex, String columnHeader) { return handle.findTableCell(rowIndex, columnHeader); }
    }

    private static List<JavaElementHandle> take(List<JavaElementHandle> items, Integer maxResults) {
        if (items == null || items.isEmpty()) return List.of();
        if (maxResults == null || maxResults < 1 || maxResults >= items.size()) return items;
        return items.subList(0, Math.min(maxResults, items.size()));
    }
}
