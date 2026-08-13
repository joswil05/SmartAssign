package com.smartassign.app.data.red

import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import com.jakewharton.retrofit2.converter.kotlinx.serialization.asConverterFactory
import retrofit2.Retrofit
import java.util.concurrent.TimeUnit
import javax.inject.Qualifier
import javax.inject.Singleton

/** Cliente sin interceptores — solo para el pre-vuelo de `/api/servidor/info` durante la alta por QR (02 §1.0). */
@Qualifier
@Retention(AnnotationRetention.BINARY)
annotation class ClienteCrudo

@Module
@InstallIn(SingletonComponent::class)
object RedModule {

    /** Marcador de posición: `InterceptorUrlServidor` reescribe host/puerto/esquema antes de que salga cualquier petición real. */
    private const val URL_MARCADOR_DE_POSICION = "http://smartassign.invalido/"

    @Provides
    @Singleton
    fun json(): Json = Json { ignoreUnknownKeys = true }

    @Provides
    @ClienteCrudo
    @Singleton
    fun clienteCrudo(): OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(5, TimeUnit.SECONDS)
        .readTimeout(5, TimeUnit.SECONDS)
        .build()

    @Provides
    @Singleton
    fun clienteHttp(
        urlServidor: InterceptorUrlServidor,
        autorizacion: InterceptorAutorizacion,
        conectividad: InterceptorConectividad
    ): OkHttpClient = OkHttpClient.Builder()
        .addInterceptor(urlServidor)
        .addInterceptor(autorizacion)
        .addInterceptor(conectividad)
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(15, TimeUnit.SECONDS)
        .build()

    @Provides
    @Singleton
    fun retrofit(cliente: OkHttpClient, json: Json): Retrofit = Retrofit.Builder()
        .baseUrl(URL_MARCADOR_DE_POSICION)
        .client(cliente)
        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
        .build()

    @Provides
    @Singleton
    fun authApi(retrofit: Retrofit): AuthApi = retrofit.create(AuthApi::class.java)

    @Provides
    @Singleton
    fun mallaApi(retrofit: Retrofit): MallaApi = retrofit.create(MallaApi::class.java)

    @Provides
    @Singleton
    fun personalApi(retrofit: Retrofit): PersonalApi = retrofit.create(PersonalApi::class.java)

    @Provides
    @Singleton
    fun asignacionApi(retrofit: Retrofit): AsignacionApi = retrofit.create(AsignacionApi::class.java)
}
