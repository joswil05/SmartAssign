package com.smartassign.app.data.conectividad

import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class ConectividadModule {
    @Binds
    @Singleton
    abstract fun conectividadRepositorio(impl: ConectividadRepositorioImpl): ConectividadRepositorio

    @Binds
    @Singleton
    abstract fun conexionTiempoReal(impl: PlantaHubConectividad): ConexionTiempoReal
}
