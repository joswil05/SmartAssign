package com.smartassign.app.data.sesion

import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class SesionModule {
    @Binds
    @Singleton
    abstract fun sesionLocal(impl: SesionLocalCifrada): SesionLocal

    @Binds
    @Singleton
    abstract fun sesionRepositorio(impl: SesionRepositorioImpl): SesionRepositorio
}
