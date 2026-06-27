package com.afsarali.jab.client;

import com.afsarali.jab.client.model.*;
import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;

import java.io.IOException;
import java.net.URI;
import java.net.URLEncoder;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.List;

public final class JabApiClient {
    private final URI baseUri;
    private final HttpClient http;
    private final ObjectMapper mapper;

    public JabApiClient(URI baseUri) {
        this(baseUri, HttpClient.newBuilder()
                .connectTimeout(Duration.ofSeconds(10))
                .build());
    }

    public JabApiClient(URI baseUri, HttpClient http) {
        this.baseUri = stripTrailingSlash(baseUri);
        this.http = http;
        this.mapper = new ObjectMapper()
                .registerModule(new JavaTimeModule())
                .disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);
    }

    public JsonNode health() {
        return get("/api/health", JsonNode.class);
    }

    public List<JavaWindow> windows() {
        return get("/api/java/windows", new TypeReference<>() {});
    }

    public DriverResult createSession(CreateSessionRequest request) {
        return post("/api/java/sessions", request, DriverResult.class);
    }

    public List<JsonNode> sessions() {
        return get("/api/java/sessions", new TypeReference<>() {});
    }

    public void deleteSession(String sessionId) {
        request("DELETE", "/api/java/sessions/" + encode(sessionId), null, JsonNode.class);
    }

    public DriverResult refreshSession(String sessionId) {
        return post("/api/java/sessions/" + encode(sessionId) + "/refresh", null, DriverResult.class);
    }

    public DriverResult sessionWindows(String sessionId) {
        return get("/api/java/sessions/" + encode(sessionId) + "/windows", DriverResult.class);
    }

    public DriverResult switchWindow(String sessionId, SwitchWindowRequest request) {
        return post("/api/java/sessions/" + encode(sessionId) + "/window", request, DriverResult.class);
    }

    public DriverResult tree(String sessionId) {
        return get("/api/java/sessions/" + encode(sessionId) + "/tree", DriverResult.class);
    }

    public DriverResult loadRepository(String sessionId, String repositoryPath) {
        return post("/api/java/sessions/" + encode(sessionId) + "/repository/load", LoadRepositoryRequest.single(repositoryPath), DriverResult.class);
    }

    public DriverResult loadRepositories(String sessionId, List<String> repositoryPaths) {
        return post("/api/java/sessions/" + encode(sessionId) + "/repository/load", LoadRepositoryRequest.multiple(repositoryPaths), DriverResult.class);
    }

    public DriverResult repository(String sessionId) {
        return get("/api/java/sessions/" + encode(sessionId) + "/repository", DriverResult.class);
    }

    public DriverResult resolveElement(String sessionId, ResolveElementRequest request) {
        return post("/api/java/sessions/" + encode(sessionId) + "/elements/resolve", request, DriverResult.class);
    }

    public DriverResult validateElement(String sessionId, JavaValidationRequest request) {
        return post("/api/java/sessions/" + encode(sessionId) + "/elements/validate", request, DriverResult.class);
    }

    public DriverResult findElements(String sessionId, JavaFindElementsRequest request) {
        return post("/api/java/sessions/" + encode(sessionId) + "/elements/find", request, DriverResult.class);
    }

    public DriverResult findChildElements(String sessionId, JavaFindChildElementsRequest request) {
        return post("/api/java/sessions/" + encode(sessionId) + "/elements/children", request, DriverResult.class);
    }

    public DriverResult executeAction(String sessionId, JavaActionRequest request) {
        return post("/api/java/sessions/" + encode(sessionId) + "/actions", request, DriverResult.class);
    }

    public DriverResult runOneShot(JavaOneShotActionRequest request) {
        return post("/api/java/actions/run", request, DriverResult.class);
    }

    public DriverResult validateOneShot(JavaValidationRequest request) {
        return post("/api/java/validate/run", request, DriverResult.class);
    }

    public DriverResult findElementsOneShot(JavaFindElementsRequest request) {
        return post("/api/java/elements/find", request, DriverResult.class);
    }

    public DriverResult findChildElementsOneShot(JavaFindChildElementsRequest request) {
        return post("/api/java/elements/children", request, DriverResult.class);
    }

    private <T> T get(String path, Class<T> responseType) {
        return request("GET", path, null, responseType);
    }

    private <T> T get(String path, TypeReference<T> responseType) {
        return request("GET", path, null, responseType);
    }

    private <T> T post(String path, Object body, Class<T> responseType) {
        return request("POST", path, body, responseType);
    }

    private <T> T request(String method, String path, Object body, Class<T> responseType) {
        try {
            return mapper.readValue(send(method, path, body), responseType);
        } catch (IOException e) {
            throw new ApiException(0, "Could not deserialize API response: " + e.getMessage());
        }
    }

    private <T> T request(String method, String path, Object body, TypeReference<T> responseType) {
        try {
            return mapper.readValue(send(method, path, body), responseType);
        } catch (IOException e) {
            throw new ApiException(0, "Could not deserialize API response: " + e.getMessage());
        }
    }

    private String send(String method, String path, Object body) {
        try {
            HttpRequest.Builder builder = HttpRequest.newBuilder(baseUri.resolve(path))
                    .timeout(Duration.ofSeconds(60))
                    .header("Accept", "application/json");

            if (body == null) {
                builder.method(method, HttpRequest.BodyPublishers.noBody());
            } else {
                builder.header("Content-Type", "application/json")
                        .method(method, HttpRequest.BodyPublishers.ofString(toJson(body), StandardCharsets.UTF_8));
            }

            HttpResponse<String> response = http.send(builder.build(), HttpResponse.BodyHandlers.ofString(StandardCharsets.UTF_8));
            if (response.statusCode() < 200 || response.statusCode() >= 300) {
                throw new ApiException(response.statusCode(), extractErrorMessage(response.body()));
            }
            return response.body();
        } catch (IOException e) {
            throw new ApiException(0, "API request failed: " + e.getMessage());
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            throw new ApiException(0, "API request interrupted.");
        }
    }

    private String toJson(Object value) throws JsonProcessingException {
        return mapper.writeValueAsString(value);
    }

    private String extractErrorMessage(String body) {
        try {
            JsonNode node = mapper.readTree(body);
            JsonNode message = node.get("message");
            return message == null ? body : message.asText();
        } catch (Exception ignored) {
            return body;
        }
    }

    private static URI stripTrailingSlash(URI uri) {
        String text = uri.toString();
        return text.endsWith("/") ? URI.create(text.substring(0, text.length() - 1)) : uri;
    }

    private static String encode(String value) {
        return URLEncoder.encode(value, StandardCharsets.UTF_8).replace("+", "%20");
    }
}
